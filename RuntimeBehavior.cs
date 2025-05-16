using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace AiSorcery
{
    public class RuntimeBehaviorObserver : MonoBehaviour
    {
        [Header("Observation Settings")]
        public bool observePlayerBehavior = true;
        public bool observeCreatureBehavior = true;
        public bool observeItemInteractions = true;
        public bool observeSpellEffects = true;

        [Header("Learning Settings")]
        public float observationInterval = 0.5f;
        public int maxObservationsPerType = 1000;
        public bool saveObservationsOnExit = true;

        // Observation data collections
        private List<PlayerActionData> playerActions = new List<PlayerActionData>();
        private List<CreatureActionData> creatureActions = new List<CreatureActionData>();
        private List<ItemInteractionData> itemInteractions = new List<ItemInteractionData>();
        private List<SpellEffectData> spellEffects = new List<SpellEffectData>();

        // Runtime instances for monitoring
        private Dictionary<string, object> monitoredInstances = new Dictionary<string, object>();

        private void Start()
        {
            StartCoroutine(ObserveBehaviors());

            // Setup event hooks
            if (observeItemInteractions)
            {
                SetupItemObservation();
            }

            if (observeSpellEffects)
            {
                SetupSpellObservation();
            }
        }

        private IEnumerator ObserveBehaviors()
        {
            while (true)
            {
                if (observePlayerBehavior && Player.local != null)
                {
                    ObservePlayer(Player.local);
                }

                if (observeCreatureBehavior)
                {
                    foreach (Creature creature in Creature.allActive)
                    {
                        if (creature != null && !creature.isPlayer)
                        {
                            ObserveCreature(creature);
                        }
                    }
                }

                yield return new WaitForSeconds(observationInterval);
            }
        }

        private void ObservePlayer(Player player)
        {
            if (playerActions.Count >= maxObservationsPerType) return;

            PlayerActionData data = new PlayerActionData
            {
                Timestamp = Time.time,
                Position = player.transform.position,
                Rotation = player.transform.rotation.eulerAngles,
                MovementDirection = player.locomotion.moveDirection,
                Velocity = player.locomotion.velocity,
                IsGrounded = player.locomotion.isGrounded,
                LeftHandItem = player.handLeft.grabbedHandle?.item?.itemId ?? "None",
                RightHandItem = player.handRight.grabbedHandle?.item?.itemId ?? "None",
                CurrentHealth = player.creature.currentHealth,
                CurrentMana = player.mana.currentMana,
                LeftSpell = player.mana.casterLeft?.currentSpellData?.id ?? "None",
                RightSpell = player.mana.casterRight?.currentSpellData?.id ?? "None"
            };

            playerActions.Add(data);
        }

        private void ObserveCreature(Creature creature)
        {
            if (creatureActions.Count >= maxObservationsPerType) return;

            CreatureActionData data = new CreatureActionData
            {
                Timestamp = Time.time,
                CreatureId = creature.creatureId,
                Position = creature.transform.position,
                Rotation = creature.transform.rotation.eulerAngles,
                MovementDirection = creature.locomotion.moveDirection,
                Velocity = creature.locomotion.velocity,
                CurrentHealth = creature.currentHealth,
                State = creature.state.ToString(),
                IsRagdolled = creature.ragdoll.state == Ragdoll.State.Destabilized.Enabled,
                CurrentBrainState = creature.brain.instance?.GetCurrentStateID() ?? "None",
                TargetPosition = creature.brain.instance?.GetTarget()?.position ?? Vector3.zero,
                DistanceToPlayer = Player.local != null ? Vector3.Distance(creature.transform.position, Player.local.transform.position) : -1f
            };

            creatureActions.Add(data);
        }

        private void SetupItemObservation()
        {
            // Subscribe to item spawn events
            EventManager.onItemSpawn += OnItemSpawned;
        }

        private void OnItemSpawned(Item item)
        {
            if (item == null) return;

            // Subscribe to item events
            item.OnGrabEvent += OnItemGrabbed;
            item.OnUngrabEvent += OnItemUngrabbed;
            item.OnCollisionEvent += OnItemCollision;
            item.OnHeldActionEvent += OnItemAction;
        }

        private void OnItemGrabbed(Handle handle, RagdollHand hand)
        {
            if (itemInteractions.Count >= maxObservationsPerType) return;

            ItemInteractionData data = new ItemInteractionData
            {
                Timestamp = Time.time,
                InteractionType = "Grab",
                ItemId = handle.item.itemId,
                HandSide = hand.side.ToString(),
                Position = handle.item.transform.position,
                Velocity = handle.item.rb.velocity
            };

            itemInteractions.Add(data);
        }

        private void OnItemUngrabbed(Handle handle, RagdollHand hand, bool throwing)
        {
            if (itemInteractions.Count >= maxObservationsPerType) return;

            ItemInteractionData data = new ItemInteractionData
            {
                Timestamp = Time.time,
                InteractionType = throwing ? "Throw" : "Release",
                ItemId = handle.item.itemId,
                HandSide = hand.side.ToString(),
                Position = handle.item.transform.position,
                Velocity = handle.item.rb.velocity
            };

            itemInteractions.Add(data);
        }

        private void OnItemCollision(CollisionInstance collision)
        {
            if (itemInteractions.Count >= maxObservationsPerType) return;

            Item item = collision.sourceColliderGroup?.collisionHandler?.item;
            if (item == null) return;

            ItemInteractionData data = new ItemInteractionData
            {
                Timestamp = Time.time,
                InteractionType = "Collision",
                ItemId = item.itemId,
                CollisionTarget = collision.targetColliderGroup?.name ?? "Unknown",
                Position = collision.contactPoint,
                Force = collision.impactVelocity.magnitude,
                DamageType = collision.damageStruct.damageType.ToString(),
                DamageAmount = collision.damageStruct.damage
            };

            itemInteractions.Add(data);
        }

        private void OnItemAction(RagdollHand hand, Handle handle, Interactable.Action action)
        {
            if (itemInteractions.Count >= maxObservationsPerType) return;

            ItemInteractionData data = new ItemInteractionData
            {
                Timestamp = Time.time,
                InteractionType = "Action",
                ActionType = action.ToString(),
                ItemId = handle.item.itemId,
                HandSide = hand.side.ToString(),
                Position = handle.item.transform.position
            };

            itemInteractions.Add(data);
        }

        private void SetupSpellObservation()
        {
            // Subscribe to spell casting events
            if (Player.local != null)
            {
                if (Player.local.mana.casterLeft != null)
                    Player.local.mana.casterLeft.OnSpellCastEvent += OnSpellCast;

                if (Player.local.mana.casterRight != null)
                    Player.local.mana.casterRight.OnSpellCastEvent += OnSpellCast;
            }
        }

        private void OnSpellCast(SpellCaster spellCaster)
        {
            if (spellEffects.Count >= maxObservationsPerType) return;

            SpellEffectData data = new SpellEffectData
            {
                Timestamp = Time.time,
                SpellId = spellCaster.currentSpellData?.id ?? "Unknown",
                HandSide = spellCaster.ragdollHand.side.ToString(),
                Position = spellCaster.transform.position,
                Direction = spellCaster.transform.forward,
                Intensity = spellCaster.intensity
            };

            spellEffects.Add(data);
        }

        private void OnApplicationQuit()
        {
            if (saveObservationsOnExit)
            {
                SaveObservations();
            }
        }

        public void SaveObservations()
        {
            string observationsPath = Path.Combine(Application.persistentDataPath, "GameBehaviorObservations");
            Directory.CreateDirectory(observationsPath);

            File.WriteAllText(Path.Combine(observationsPath, "PlayerActions.json"), JsonUtility.ToJson(new PlayerActionCollection { actions = playerActions }));
            File.WriteAllText(Path.Combine(observationsPath, "CreatureActions.json"), JsonUtility.ToJson(new CreatureActionCollection { actions = creatureActions }));
            File.WriteAllText(Path.Combine(observationsPath, "ItemInteractions.json"), JsonUtility.ToJson(new ItemInteractionCollection { interactions = itemInteractions }));
            File.WriteAllText(Path.Combine(observationsPath, "SpellEffects.json"), JsonUtility.ToJson(new SpellEffectCollection { effects = spellEffects }));

            Debug.Log($"Saved {playerActions.Count} player actions, {creatureActions.Count} creature actions, " +
                      $"{itemInteractions.Count} item interactions, and {spellEffects.Count} spell effects.");
        }

        // Data structure classes
        [Serializable]
        public class PlayerActionData
        {
            public float Timestamp;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 MovementDirection;
            public Vector3 Velocity;
            public bool IsGrounded;
            public string LeftHandItem;
            public string RightHandItem;
            public float CurrentHealth;
            public float CurrentMana;
            public string LeftSpell;
            public string RightSpell;
        }

        [Serializable]
        public class PlayerActionCollection
        {
            public List<PlayerActionData> actions;
        }

        [Serializable]
        public class CreatureActionData
        {
            public float Timestamp;
            public string CreatureId;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 MovementDirection;
            public Vector3 Velocity;
            public float CurrentHealth;
            public string State;
            public bool IsRagdolled;
            public string CurrentBrainState;
            public Vector3 TargetPosition;
            public float DistanceToPlayer;
        }

        [Serializable]
        public class CreatureActionCollection
        {
            public List<CreatureActionData> actions;
        }

        [Serializable]
        public class ItemInteractionData
        {
            public float Timestamp;
            public string InteractionType;
            public string ActionType;
            public string ItemId;
            public string HandSide;
            public Vector3 Position;
            public Vector3 Velocity;
            public string CollisionTarget;
            public float Force;
            public string DamageType;
            public float DamageAmount;
        }

        [Serializable]
        public class ItemInteractionCollection
        {
            public List<ItemInteractionData> interactions;
        }

        [Serializable]
        public class SpellEffectData
        {
            public float Timestamp;
            public string SpellId;
            public string HandSide;
            public Vector3 Position;
            public Vector3 Direction;
            public float Intensity;
        }

        [Serializable]
        public class SpellEffectCollection
        {
            public List<SpellEffectData> effects;
        }
    }
}
