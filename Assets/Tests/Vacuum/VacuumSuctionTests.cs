using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hedgewitch.Vacuum;
using Hedgewitch.Vacuum.Enemies;
using Hedgewitch.Vacuum.Objects;

namespace Hedgewitch.Vacuum.Tests
{
    /// <summary>
    /// Play mode checks on the claims the vacuum makes: that it pulls with real forces, that the
    /// cone and walls actually bound it, that leaving the airflow stops the pull and restores the
    /// object, and that reaching the mouth captures.
    /// </summary>
    public class VacuumSuctionTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private VacuumController controller;
        private VacuumNozzle nozzle;
        private VacuumSettings settings;

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

            if (settings != null)
            {
                Object.DestroyImmediate(settings);
                settings = null;
            }

            controller = null;
            nozzle = null;
            VacuumRegistry.Clear();
        }

        private void CreateVacuum(float baseForce = 60f)
        {
            GameObject rig = new GameObject("Test Rig");
            spawned.Add(rig);

            GameObject nozzleHost = new GameObject("Test Nozzle");
            nozzleHost.transform.SetParent(rig.transform, false);
            nozzle = nozzleHost.AddComponent<VacuumNozzle>();

            settings = VacuumSettings.CreateDefault();
            settings.baseForce = baseForce;
            settings.rescanInterval = 0f;

            controller = rig.AddComponent<VacuumController>();
            controller.Settings = settings;
        }

        private GameObject CreateProp(
            Vector3 position,
            float vacuumMass,
            bool capturable,
            float swirl = 0f,
            float wobble = 0f,
            float funnel = 1f)
        {
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = "Test Prop";
            prop.transform.position = position;
            prop.transform.localScale = Vector3.one * 0.3f;
            spawned.Add(prop);

            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;

            VacuumableProfile profile = ScriptableObject.CreateInstance<VacuumableProfile>();
            profile.category = VacuumCategory.SmallProp;
            profile.vacuumMass = vacuumMass;
            profile.canBeCaptured = capturable;
            profile.overrideRigidbody = false;
            profile.wobbleMultiplier = wobble;
            profile.swirlMultiplier = swirl;
            profile.funnelMultiplier = funnel;
            profile.captureOutcome = VacuumCaptureOutcome.Deactivate;

            VacuumableBody vacuumable = prop.AddComponent<VacuumableBody>();
            vacuumable.ApplyProfile(profile);

            return prop;
        }

        private EnemySuctionResponse CreateEnemy(Vector3 position)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = "Test Enemy";
            enemy.transform.position = position;
            spawned.Add(enemy);

            Rigidbody body = enemy.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 45f;
            body.freezeRotation = true;

            return enemy.AddComponent<EnemySuctionResponse>();
        }

        private static IEnumerator Step(int fixedFrames)
        {
            for (int i = 0; i < fixedFrames; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator Suction_pulls_a_prop_toward_the_nozzle()
        {
            CreateVacuum();
            GameObject prop = CreateProp(new Vector3(0f, 0f, 4f), vacuumMass: 20f, capturable: false);

            yield return null;
            float startDistance = Vector3.Distance(prop.transform.position, nozzle.Origin);

            controller.BeginSuction();
            yield return Step(60);

            float endDistance = Vector3.Distance(prop.transform.position, nozzle.Origin);

            Assert.Less(endDistance, startDistance - 0.5f,
                $"Prop should have been pulled in. start={startDistance:F2} end={endDistance:F2}");
            Assert.Greater(controller.ActiveTargetCount, 0, "Prop should be tracked while in the cone.");
        }

        [UnityTest]
        public IEnumerator Lighter_props_are_pulled_harder_than_heavy_ones()
        {
            CreateVacuum();
            GameObject light = CreateProp(new Vector3(-1f, 0f, 4f), vacuumMass: 2f, capturable: false);
            GameObject heavy = CreateProp(new Vector3(1f, 0f, 4f), vacuumMass: 200f, capturable: false);

            yield return null;
            float lightStart = light.transform.position.z;
            float heavyStart = heavy.transform.position.z;

            controller.BeginSuction();
            yield return Step(40);

            float lightMoved = lightStart - light.transform.position.z;
            float heavyMoved = heavyStart - heavy.transform.position.z;

            Assert.Greater(lightMoved, heavyMoved,
                $"vacuumMass should govern how hard a pull lands. light={lightMoved:F2} heavy={heavyMoved:F2}");
        }

        [UnityTest]
        public IEnumerator A_wall_blocks_the_airflow()
        {
            CreateVacuum();
            GameObject prop = CreateProp(new Vector3(0f, 0f, 4f), vacuumMass: 20f, capturable: false);

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test Wall";
            wall.transform.position = new Vector3(0f, 0f, 2f);
            wall.transform.localScale = new Vector3(6f, 6f, 0.4f);
            spawned.Add(wall);

            yield return null;
            float startDistance = Vector3.Distance(prop.transform.position, nozzle.Origin);

            controller.BeginSuction();
            yield return Step(60);

            float endDistance = Vector3.Distance(prop.transform.position, nozzle.Origin);

            Assert.AreEqual(0, controller.ActiveTargetCount, "Nothing behind a wall should be tracked.");
            Assert.That(endDistance, Is.EqualTo(startDistance).Within(0.05f),
                "A prop behind a wall should not be pulled at all.");
        }

        [UnityTest]
        public IEnumerator Objects_outside_the_cone_are_not_affected()
        {
            CreateVacuum();
            GameObject behind = CreateProp(new Vector3(0f, 0f, -4f), vacuumMass: 20f, capturable: false);

            yield return null;
            Vector3 startPosition = behind.transform.position;

            controller.BeginSuction();
            yield return Step(40);

            Assert.AreEqual(0, controller.ActiveTargetCount, "A prop behind the nozzle is outside the cone.");
            Assert.That(Vector3.Distance(behind.transform.position, startPosition), Is.LessThan(0.05f),
                "A prop outside the cone should not move.");
        }

        [UnityTest]
        public IEnumerator Leaving_the_cone_releases_the_object()
        {
            CreateVacuum();
            GameObject prop = CreateProp(new Vector3(0f, 0f, 4f), vacuumMass: 40f, capturable: false);
            VacuumableBody vacuumable = prop.GetComponent<VacuumableBody>();

            controller.BeginSuction();
            yield return Step(10);

            Assert.IsTrue(vacuumable.IsBeingSucked, "Prop should be in the airflow to start with.");

            // Swing the nozzle away.
            for (int i = 0; i < 30; i++)
            {
                nozzle.AimTowards(Vector3.back);
                yield return new WaitForFixedUpdate();
            }

            Assert.IsFalse(vacuumable.IsBeingSucked, "Prop should have been released once out of the cone.");
            Assert.AreEqual(0, controller.ActiveTargetCount);
        }

        [UnityTest]
        public IEnumerator Releasing_the_trigger_restores_the_rigidbody()
        {
            CreateVacuum();
            GameObject prop = CreateProp(new Vector3(0f, 0f, 4f), vacuumMass: 40f, capturable: false);
            Rigidbody body = prop.GetComponent<Rigidbody>();
            VacuumableBody vacuumable = prop.GetComponent<VacuumableBody>();

            float originalDamping = body.linearDamping;

            controller.BeginSuction();
            yield return Step(10);

            Assert.Greater(body.linearDamping, originalDamping, "Extra damping is applied while sucked.");

            controller.EndSuction();
            yield return Step(2);

            Assert.IsFalse(vacuumable.IsBeingSucked);
            Assert.IsFalse(body.isKinematic, "Releasing must never leave an object out of the simulation.");
            Assert.That(body.linearDamping, Is.EqualTo(originalDamping).Within(0.0001f),
                "Damping should be restored exactly on release.");
        }

        [UnityTest]
        public IEnumerator Reaching_the_mouth_captures_a_capturable_object()
        {
            CreateVacuum();
            GameObject prop = CreateProp(new Vector3(0f, 0f, 1.5f), vacuumMass: 1f, capturable: true);

            bool captured = false;
            controller.ObjectCaptured += _ => captured = true;

            controller.BeginSuction();
            yield return Step(90);

            Assert.IsTrue(captured, "A light capturable prop should be swallowed within 1.8 s.");
            Assert.IsFalse(prop.activeSelf, "This prop's outcome is Deactivate, so it should be inactive.");
        }

        [UnityTest]
        public IEnumerator Objects_that_cannot_be_captured_stay_in_the_world()
        {
            CreateVacuum();
            GameObject prop = CreateProp(new Vector3(0f, 0f, 1.5f), vacuumMass: 1f, capturable: false);

            bool captured = false;
            controller.ObjectCaptured += _ => captured = true;

            controller.BeginSuction();
            yield return Step(90);

            Assert.IsFalse(captured, "canBeCaptured=false must never be swallowed.");
            Assert.IsTrue(prop.activeSelf);
            Assert.IsFalse(prop.GetComponent<VacuumableBody>().IsCaptured);
        }

        /// <summary>
        /// Regression: the nozzle sits above floor level, so with a narrow cone a small object
        /// nearby was under the cone entirely and the player had to tip the mouth down at it before
        /// anything happened.
        /// </summary>
        [UnityTest]
        public IEnumerator A_small_object_below_a_level_nozzle_is_still_drawn_in()
        {
            CreateVacuum();

            // Low and close: steeply under the cone's vertical angle, but within the intake.
            GameObject prop = CreateProp(new Vector3(0f, -1f, 1f), vacuumMass: 0.3f, capturable: false);

            yield return null;
            float startDistance = Vector3.Distance(prop.transform.position, nozzle.Origin);

            controller.BeginSuction();
            yield return Step(40);

            Assert.Greater(controller.ActiveTargetCount, 0,
                "The intake should reach an object just under the mouth without aiming at it.");
            Assert.Less(Vector3.Distance(prop.transform.position, nozzle.Origin), startDistance - 0.2f,
                "A small object below a level nozzle should be pulled in.");
        }

        [UnityTest]
        public IEnumerator The_intake_does_not_reach_past_its_radius()
        {
            CreateVacuum();

            // Same steep angle, but well beyond the intake radius.
            GameObject prop = CreateProp(new Vector3(0f, -3.5f, 1f), vacuumMass: 0.3f, capturable: false);

            yield return null;
            Vector3 startPosition = prop.transform.position;

            controller.BeginSuction();
            yield return Step(40);

            Assert.AreEqual(0, controller.ActiveTargetCount,
                "Steeply below the cone and outside the intake radius should not be reachable.");
            Assert.That(Vector3.Distance(prop.transform.position, startPosition), Is.LessThan(0.05f));
        }

        /// <summary>
        /// Regression: an off-axis light prop used to be slung around the mouth and past the player
        /// instead of being drawn into the intake.
        /// </summary>
        [UnityTest]
        public IEnumerator A_light_offset_prop_funnels_into_the_mouth_without_being_slung_past()
        {
            CreateVacuum();
            GameObject prop = CreateProp(
                new Vector3(1.5f, 0f, 3f), vacuumMass: 0.15f, capturable: true, swirl: 1.5f, wobble: 1f);

            bool captured = false;
            controller.ObjectCaptured += _ => captured = true;

            controller.BeginSuction();

            float closest = float.MaxValue;
            float worstRebound = 0f;

            for (int i = 0; i < 180 && !captured; i++)
            {
                yield return new WaitForFixedUpdate();
                if (prop == null || !prop.activeSelf)
                {
                    break;
                }

                float distance = Vector3.Distance(prop.transform.position, nozzle.Origin);
                closest = Mathf.Min(closest, distance);

                // Once it has come close, any growth in distance is it being thrown back out.
                if (closest < 1.5f)
                {
                    worstRebound = Mathf.Max(worstRebound, distance - closest);
                }
            }

            Assert.IsTrue(captured, "A light prop in the cone should end up inside the vacuum.");
            Assert.Less(worstRebound, 0.6f,
                $"Prop should be funnelled into the intake, not slung past it. rebound={worstRebound:F2} m");
        }

        /// <summary>
        /// Regression: resistance was applied as an unconditional counter-force, so an enemy
        /// stronger than the airflow was actively shoved away from the nozzle.
        /// </summary>
        [UnityTest]
        public IEnumerator A_braced_enemy_holds_its_ground_instead_of_being_pushed_away()
        {
            // Weak airflow against a default enemy (strength 45) so resistance exceeds the pull,
            // which is exactly the case that used to repel the enemy.
            CreateVacuum(baseForce: 20f);
            EnemySuctionResponse enemy = CreateEnemy(new Vector3(0f, 0f, 3f));

            yield return null;
            float startDistance = Vector3.Distance(enemy.transform.position, nozzle.Origin);

            controller.BeginSuction();
            yield return Step(40);

            float endDistance = Vector3.Distance(enemy.transform.position, nozzle.Origin);

            Assert.Less(endDistance, startDistance + 0.15f,
                $"A braced enemy must not be shoved away. start={startDistance:F2} end={endDistance:F2}");
            Assert.AreEqual(EnemySuctionStage.Resisting, enemy.Stage, "It should be digging in, not losing.");
        }

        [UnityTest]
        public IEnumerator Sustained_suction_wears_an_enemy_down_and_overpowers_it()
        {
            CreateVacuum(baseForce: 20f);
            EnemySuctionResponse enemy = CreateEnemy(new Vector3(0f, 0f, 3f));

            yield return null;
            Assert.That(enemy.StaminaNormalized, Is.EqualTo(1f).Within(0.01f));

            bool captured = false;
            bool overpowered = false;
            float lowestStamina = 1f;
            controller.ObjectCaptured += _ => captured = true;

            controller.BeginSuction();

            // Eight seconds is a generous ceiling; it should take about four.
            for (int i = 0; i < 400 && !captured && !overpowered; i++)
            {
                yield return new WaitForFixedUpdate();
                lowestStamina = Mathf.Min(lowestStamina, enemy.StaminaNormalized);
                overpowered = enemy.Grip >= 0.85f;
            }

            Assert.Less(lowestStamina, 0.05f, "Resisting should have exhausted it.");
            Assert.IsTrue(overpowered || captured,
                "Sustained suction should overpower an enemy rather than stalling forever.");
        }

        [UnityTest]
        public IEnumerator Suction_spools_up_and_down()
        {
            CreateVacuum();

            yield return null;
            Assert.AreEqual(VacuumState.Idle, controller.State);
            Assert.That(controller.Intensity, Is.EqualTo(0f).Within(0.001f));

            controller.BeginSuction();
            yield return Step(20);

            Assert.AreEqual(VacuumState.Sucking, controller.State);
            Assert.That(controller.Intensity, Is.EqualTo(1f).Within(0.001f), "Should be fully spooled after 0.4 s.");

            controller.EndSuction();
            yield return Step(30);

            Assert.AreEqual(VacuumState.Idle, controller.State);
            Assert.That(controller.Intensity, Is.EqualTo(0f).Within(0.001f), "Should be fully spooled down.");
        }
    }
}
