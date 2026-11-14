// Play mode tests for broom strings musket reload weapon switching and hit reactions
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hedgewitch.Vacuum;

namespace Hedgewitch.Combat.Tests
{
    // Fake input aim and body for tests
    public class TestCombatDriver : MonoBehaviour, ICombatInput, ICombatAim, ICombatMotor
    {
        public bool HoldPrimary;
        public Vector3 AimOffset = Vector3.forward * 5f;

        private bool primaryQueued;
        private bool secondaryQueued;
        private int slotQueued = -1;

        public bool HasAim => true;

        public Vector3 AimPoint => transform.position + AimOffset;

        public void PressPrimary() => primaryQueued = true;

        public void PressSecondary() => secondaryQueued = true;

        public void SelectSlot(int index) => slotQueued = index;

        public CombatInputFrame ReadCombatInput()
        {
            CombatInputFrame frame = new CombatInputFrame
            {
                PrimaryPressed = primaryQueued,
                PrimaryHeld = HoldPrimary || primaryQueued,
                SecondaryPressed = secondaryQueued,
                SecondaryHeld = secondaryQueued,
                SelectSlot = slotQueued,
                CycleSlot = 0
            };

            primaryQueued = false;
            secondaryQueued = false;
            slotQueued = -1;
            return frame;
        }

        public void HoldFacing(Vector3 direction, float seconds)
        {
        }

        public void HoldMoveScale(float scale, float seconds)
        {
        }

        public void Lunge(Vector3 velocity, float seconds)
        {
        }
    }

    // Fake AI brain to check stagger switches it off
    public class TestBrain : MonoBehaviour
    {
    }

    public class CombatTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<DamageInfo> hits = new List<DamageInfo>();

        private GameObject player;
        private TestCombatDriver driver;
        private WeaponInventory inventory;

        [SetUp]
        public void SetUp()
        {
            HitStop.Enabled = false;
            hits.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Object.DestroyImmediate(spawned[i]);
                }
            }

            spawned.Clear();
            HitStop.Enabled = true;
            Time.timeScale = 1f;
            VacuumRegistry.Clear();
        }

        private void CreatePlayer()
        {
            player = new GameObject("Test Player");
            spawned.Add(player);

            driver = player.AddComponent<TestCombatDriver>();
            inventory = player.AddComponent<WeaponInventory>();
            inventory.SetSources(driver, driver, driver);
            inventory.HitLanded += (weapon, target, info, result) => hits.Add(info);
        }

        private T AddWeapon<T>(string name) where T : Weapon
        {
            GameObject host = new GameObject(name);
            host.transform.SetParent(player.transform, false);
            T weapon = host.AddComponent<T>();
            inventory.AddWeapon(weapon);
            return weapon;
        }

        private (Rigidbody body, Health health, HitReaction reaction) CreateEnemy(Vector3 position, float maxHealth = 1000f)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Test Enemy";
            enemy.transform.position = position;
            spawned.Add(enemy);

            Rigidbody body = enemy.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.freezeRotation = true;
            body.mass = 45f;
            body.linearDamping = 3f;

            Health health = enemy.AddComponent<Health>();
            health.SetMaxHealth(maxHealth);
            HitReaction reaction = enemy.AddComponent<HitReaction>();
            return (body, health, reaction);
        }

        private IEnumerator WaitForHits(int count, float timeout = 1.5f)
        {
            float start = Time.time;
            while (hits.Count < count && Time.time - start < timeout)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Three_light_swings_chain_and_the_last_knocks_the_enemy_up_and_back()
        {
            CreatePlayer();
            BroomWeapon broom = AddWeapon<BroomWeapon>("Broom");
            (Rigidbody body, Health health, _) = CreateEnemy(new Vector3(0f, 0f, 1.2f));
            yield return null;
            inventory.Equip(0);

            driver.PressPrimary();
            yield return WaitForHits(1);
            Assert.AreEqual(1, hits.Count, "the first swing should land");
            Assert.IsFalse(hits[0].HasTag(DamageTags.Finisher), "the first swing is not a finisher");
            Assert.AreEqual(0f, hits[0].Launch, "ordinary swings should not knock up");
            Assert.Less(body.linearVelocity.y, 0.01f);

            driver.PressPrimary();
            yield return WaitForHits(2);
            Assert.AreEqual(2, hits.Count, "the second swing should chain from the first");
            Assert.AreEqual(1, broom.ComboIndex, "the string should be on its second step");

            driver.PressPrimary();
            yield return WaitForHits(3);
            Assert.AreEqual(3, hits.Count, "the third swing should chain from the second");
            Assert.IsTrue(hits[2].HasTag(DamageTags.Finisher), "the third light swing is the finisher");

            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.y, 1.5f, "the finisher should knock the enemy up");
            Assert.Greater(body.linearVelocity.z, 2f, "the finisher should knock the enemy back, away from the witch");
            Assert.Less(health.Current, health.Max, "the hits should have hurt");
        }

        [UnityTest]
        public IEnumerator The_heavy_string_ends_in_a_finisher_too()
        {
            CreatePlayer();
            AddWeapon<BroomWeapon>("Broom");
            (Rigidbody body, _, _) = CreateEnemy(new Vector3(0f, 0f, 1.2f));
            yield return null;
            inventory.Equip(0);

            driver.PressSecondary();
            yield return WaitForHits(1);
            Assert.AreEqual(1, hits.Count);
            Assert.IsTrue(hits[0].HasTag(DamageTags.Heavy));
            Assert.IsFalse(hits[0].HasTag(DamageTags.Finisher));

            driver.PressSecondary();
            yield return WaitForHits(2);
            Assert.AreEqual(2, hits.Count);
            Assert.IsTrue(hits[1].HasTag(DamageTags.Finisher), "the second heavy swing is the finisher");

            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.y, 1.5f, "the heavy finisher should knock the enemy up");
        }

        [UnityTest]
        public IEnumerator A_swing_does_not_reach_behind_the_witch()
        {
            CreatePlayer();
            AddWeapon<BroomWeapon>("Broom");
            CreateEnemy(new Vector3(0f, 0f, -1.4f));
            yield return null;
            inventory.Equip(0);

            driver.PressPrimary();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0, hits.Count, "an enemy behind the swing should not be hit");
        }

        [UnityTest]
        public IEnumerator The_musket_fires_once_then_must_reload()
        {
            CreatePlayer();
            MusketWeapon musket = AddWeapon<MusketWeapon>("Musket");
            musket.ReloadTime = 0.4f;
            int dryFires = 0;
            musket.DryFired += _ => dryFires++;

            (_, Health health, _) = CreateEnemy(new Vector3(0f, 0f, 8f));
            yield return null;
            inventory.Equip(0);

            driver.PressPrimary();
            yield return WaitForHits(1);
            Assert.AreEqual(1, hits.Count, "the ball should hit the enemy");
            Assert.IsTrue(hits[0].HasTag(DamageTags.Ranged));
            Assert.AreEqual(health.Max - musket.Damage, health.Current, 0.01f);
            Assert.IsFalse(musket.IsLoaded, "firing should empty the musket");

            driver.PressPrimary();
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(1, hits.Count, "an empty musket should not fire");
            Assert.AreEqual(1, dryFires, "pulling the trigger on an empty musket should only click");

            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(musket.IsLoaded, "the musket should have reloaded");

            driver.PressPrimary();
            yield return WaitForHits(2);
            Assert.AreEqual(2, hits.Count, "a reloaded musket should fire again");
        }

        [UnityTest]
        public IEnumerator Number_keys_swap_the_weapon_in_hand()
        {
            CreatePlayer();

            GameObject rig = new GameObject("Test Vacuum Rig");
            rig.transform.SetParent(player.transform, false);
            GameObject nozzleHost = new GameObject("Test Nozzle");
            nozzleHost.transform.SetParent(rig.transform, false);
            nozzleHost.AddComponent<VacuumNozzle>();
            VacuumSettings settings = VacuumSettings.CreateDefault();
            VacuumController controller = rig.AddComponent<VacuumController>();
            controller.Settings = settings;

            BroomWeapon broom = AddWeapon<BroomWeapon>("Broom");
            MusketWeapon musket = AddWeapon<MusketWeapon>("Musket");
            VacuumWeapon vacuum = AddWeapon<VacuumWeapon>("Vacuum");
            Assert.AreEqual(controller, vacuum.Controller, "the vacuum weapon should find the player's vacuum");

            yield return null;
            Assert.AreEqual(broom, inventory.Current, "slot 1 is in hand at the start");

            driver.SelectSlot(1);
            yield return null;
            Assert.AreEqual(musket, inventory.Current, "key 2 should take up the musket");
            Assert.IsTrue(musket.IsEquipped);
            Assert.IsFalse(broom.IsEquipped);

            driver.SelectSlot(2);
            yield return null;
            Assert.AreEqual(vacuum, inventory.Current, "key 3 should take up the vacuum");

            driver.HoldPrimary = true;
            yield return null;
            yield return null;
            Assert.IsTrue(controller.IsSucking, "holding the primary button with the vacuum out should suck");

            driver.SelectSlot(0);
            yield return null;
            Assert.AreEqual(broom, inventory.Current, "key 1 should take the broom back up");
            Assert.IsFalse(controller.IsSucking, "putting the vacuum away should stop it");

            yield return new WaitForSeconds(0.1f);
            Assert.IsFalse(controller.IsSucking, "the vacuum should stay off while the broom is in hand");

            Object.DestroyImmediate(settings);
        }

        [UnityTest]
        public IEnumerator Defeat_is_reported_and_the_enemy_is_tidied_away()
        {
            CreatePlayer();
            MusketWeapon musket = AddWeapon<MusketWeapon>("Musket");
            (_, Health health, _) = CreateEnemy(new Vector3(0f, 0f, 6f), maxHealth: 30f);

            int defeats = 0;
            inventory.Defeated += (weapon, target, info) => defeats++;

            yield return null;
            inventory.Equip(0);

            driver.PressPrimary();
            yield return WaitForHits(1);
            Assert.AreEqual(1, defeats, "a lethal hit should be reported as a defeat");
            Assert.IsFalse(health.IsAlive);

            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(health.gameObject.activeSelf, "a defeated enemy should be removed after its short delay");
        }

        [UnityTest]
        public IEnumerator A_stagger_switches_the_brain_off_and_back_on()
        {
            (_, _, HitReaction reaction) = CreateEnemy(Vector3.zero);
            TestBrain brain = reaction.gameObject.AddComponent<TestBrain>();
            reaction.SetStaggerBehaviours(brain);

            reaction.Stagger(0.2f);
            Assert.IsFalse(brain.enabled, "a staggered enemy should lose its brain");
            Assert.IsTrue(reaction.IsStaggered);

            yield return new WaitForSeconds(0.35f);
            Assert.IsTrue(brain.enabled, "the brain should come back when the stagger ends");
            Assert.IsFalse(reaction.IsStaggered);
        }

        [UnityTest]
        public IEnumerator Weapon_modifiers_change_damage_on_the_next_hit()
        {
            CreatePlayer();
            MusketWeapon musket = AddWeapon<MusketWeapon>("Musket");
            musket.AddModifier(new DoubleDamage());
            CreateEnemy(new Vector3(0f, 0f, 6f));
            yield return null;
            inventory.Equip(0);

            driver.PressPrimary();
            yield return WaitForHits(1);
            Assert.AreEqual(musket.Damage * 2f, hits[0].Amount, 0.01f, "a modifier should scale the hit");
        }

        private class DoubleDamage : IWeaponModifier
        {
            public void ModifyStats(Weapon weapon, ref WeaponStats stats)
            {
                stats.DamageMultiplier *= 2f;
            }
        }
    }
}
