using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SugarRush
{
    /// <summary>Compact kart snapshot sent by the kart's owner (~46 bytes).</summary>
    public struct KartNetState : INetworkSerializeByMemcpy
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float ForwardSpeed;
        public float Steer;
        public byte Flags; // 1 grounded, 2 drifting, 4 boosting
        public sbyte DriftDirection;
    }

    /// <summary>
    /// Online kart. Whoever owns it (the player's own machine, or the host for AI) simulates it
    /// with the normal KartController and sends a snapshot ~20 times a second. Everyone else
    /// shows a kinematic "proxy" that interpolates between snapshots ~0.12 s in the past.
    /// Bumping into a proxy gives a simple push instead of exact physics.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class NetKart : NetworkBehaviour
    {
        public float sendRate = 20f;
        public float interpolationDelay = 0.12f;
        public float maxExtrapolation = 0.25f;
        public float teleportDistance = 12f;
        public float pushStrength = 5f;

        public readonly NetworkVariable<KartNetState> State =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<int> Character = new();
        public readonly NetworkVariable<bool> Human = new();
        public readonly NetworkVariable<ulong> HumanClientId = new();
        public readonly NetworkVariable<int> PlayerNumber = new();
        /// <summary>Ralph's chaos: gold coins carried, counted by the host.</summary>
        public readonly NetworkVariable<int> Coins = new();

        public KartController Kart { get; private set; }
        public RaceProgress Progress { get; set; }
        /// <summary>This machine's own human kart.</summary>
        public bool IsMine => IsSpawned && IsOwner && Human.Value && HumanClientId.Value == NetworkManager.LocalClientId;

        struct Setup { public int Character; public bool Human; public ulong HumanClientId; public int PlayerNumber; }
        Setup? pendingSetup;

        /// <summary>Host: values to publish when the kart spawns (NetworkVariables must be set once spawned).</summary>
        public void Configure(int character, bool human, ulong humanClientId, int playerNumber) =>
            pendingSetup = new Setup { Character = character, Human = human, HumanClientId = humanClientId, PlayerNumber = playerNumber };

        struct Snapshot { public float Time; public KartNetState State; }
        readonly List<Snapshot> snapshots = new();
        float sendTimer, pushCooldown;

        void Awake() => Kart = GetComponent<KartController>();

        public override void OnNetworkSpawn()
        {
            if (IsServer && pendingSetup.HasValue)
            {
                var setup = pendingSetup.Value;
                Character.Value = setup.Character;
                Human.Value = setup.Human;
                HumanClientId.Value = setup.HumanClientId;
                PlayerNumber.Value = setup.PlayerNumber;
                pendingSetup = null;
            }
            State.OnValueChanged += OnStateChanged;
            Human.OnValueChanged += (_, _) => ApplyAuthority();
            RaceManager.Instance?.RegisterNetKart(this);
            ApplyAuthority();
        }

        public override void OnNetworkDespawn()
        {
            State.OnValueChanged -= OnStateChanged;
            RaceManager.Instance?.UnregisterNetKart(this);
        }

        public override void OnGainedOwnership() => ApplyAuthority();
        public override void OnLostOwnership() => ApplyAuthority();

        /// <summary>Owner simulates (player input or AI); everyone else shows a smooth proxy.</summary>
        public void ApplyAuthority()
        {
            if (!IsSpawned) return;
            bool simulate = IsOwner;
            Kart.SetProxy(!simulate);
            if (Progress) Progress.hasAuthority = simulate;

            var input = GetComponent<PlayerKartInput>();
            bool playerHere = simulate && Human.Value && !(Progress && Progress.Finished);
            if (playerHere && !input) gameObject.AddComponent<PlayerKartInput>();
            else if (!playerHere && input) Destroy(input);

            var ai = GetComponent<AIKartDriver>();
            bool aiHere = simulate && !Human.Value;
            if (aiHere && !ai) RaceManager.Instance?.AddAIDriver(this);
            else if (!aiHere && ai && !(Progress && Progress.Finished)) Destroy(ai);

            snapshots.Clear();
            RaceManager.Instance?.RefreshNetKart(this);
        }

        void FixedUpdate()
        {
            if (!IsSpawned) return;
            pushCooldown -= Time.fixedDeltaTime;
            if (!IsOwner) return;

            sendTimer -= Time.fixedDeltaTime;
            if (sendTimer > 0f) return;
            sendTimer = 1f / sendRate;
            var rb = Kart.Body;
            State.Value = new KartNetState
            {
                Position = rb.position,
                Rotation = rb.rotation,
                Velocity = rb.linearVelocity,
                ForwardSpeed = Kart.ForwardSpeed,
                Steer = Kart.Steer,
                Flags = (byte)((Kart.IsGrounded ? 1 : 0) | (Kart.IsDrifting ? 2 : 0) | (Kart.IsBoosting ? 4 : 0)),
                DriftDirection = (sbyte)Kart.DriftDirection,
            };
        }

        void OnStateChanged(KartNetState previous, KartNetState current)
        {
            if (IsOwner) return;
            // A big jump means a teleport (back to track): don't slide across the map.
            if (snapshots.Count > 0 && (snapshots[^1].State.Position - current.Position).sqrMagnitude > teleportDistance * teleportDistance)
                snapshots.Clear();
            snapshots.Add(new Snapshot { Time = Time.time, State = current });
            if (snapshots.Count > 12) snapshots.RemoveAt(0);
        }

        void Update()
        {
            if (!IsSpawned || IsOwner || snapshots.Count == 0) return;
            float renderTime = Time.time - interpolationDelay;

            // Drop snapshots that are entirely in the past, keeping one before renderTime.
            while (snapshots.Count > 2 && snapshots[1].Time <= renderTime) snapshots.RemoveAt(0);

            var a = snapshots[0];
            Vector3 position;
            Quaternion rotation;
            KartNetState latest = snapshots[^1].State;
            if (snapshots.Count >= 2 && renderTime <= snapshots[1].Time)
            {
                var b = snapshots[1];
                float t = Mathf.InverseLerp(a.Time, b.Time, renderTime);
                position = Vector3.Lerp(a.State.Position, b.State.Position, t);
                rotation = Quaternion.Slerp(a.State.Rotation, b.State.Rotation, t);
                latest = t < 0.5f ? a.State : b.State;
            }
            else
            {
                // Late packet: carry on along the last known velocity for a moment.
                var last = snapshots[^1];
                float ahead = Mathf.Clamp(renderTime - last.Time, 0f, maxExtrapolation);
                position = last.State.Position + last.State.Velocity * ahead;
                rotation = last.State.Rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
            Kart.Body.position = position;
            Kart.Body.rotation = rotation;
            Kart.ApplyProxyState(latest.ForwardSpeed, latest.Velocity.magnitude, latest.Steer,
                (latest.Flags & 1) != 0, (latest.Flags & 2) != 0, latest.DriftDirection, (latest.Flags & 4) != 0);
        }

        /// <summary>Simplified kart-to-kart contact: a kart we simulate gets nudged away from a proxy.</summary>
        void OnTriggerStay(Collider other)
        {
            if (!IsSpawned || !IsOwner || pushCooldown > 0f) return;
            var otherKart = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<NetKart>() : null;
            if (!otherKart || otherKart == this || otherKart.IsOwner) return;

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            Kart.Body.AddForce(away.normalized * pushStrength, ForceMode.VelocityChange);
            pushCooldown = 0.35f;

            var lib = SoundLibrary.Instance;
            if (lib && lib.crashes != null && lib.crashes.Length > 0)
                AudioHub.PlayAt(lib.crashes[Random.Range(0, lib.crashes.Length)], transform.position, 0.6f);
        }
    }
}
