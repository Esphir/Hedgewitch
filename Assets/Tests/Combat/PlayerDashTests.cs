// Play mode tests for the dash on the real Player prefab with each weapon look and its timing rules
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Hedgewitch.Vacuum.Demo;

namespace Hedgewitch.Combat.Tests
{
    public class PlayerDashTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private readonly List<Object> spawned = new List<Object>();

        private GameObject player;
        private PlayerDash dash;
        private DashAnimator look;
        private WeaponInventory inventory;
        private Health health;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawned.Add(floor);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);

            GameObject view = new GameObject("Test Camera");
            spawned.Add(view);
            view.tag = "MainCamera";
            view.AddComponent<Camera>();
            view.AddComponent<AudioListener>();
            view.transform.SetPositionAndRotation(new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f));

#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player prefab is missing");
            player = Object.Instantiate(prefab, new Vector3(0f, 1.05f, 0f), Quaternion.identity);
            spawned.Add(player);
#endif
            dash = player.GetComponent<PlayerDash>();
            look = player.GetComponent<DashAnimator>();
            inventory = player.GetComponent<WeaponInventory>();
            health = player.GetComponent<Health>();
            Assert.IsNotNull(dash, "run Tools > Hedgewitch > Player > Set Up Dash");
            Assert.IsNotNull(look);

            yield return null;
            yield return new WaitForFixedUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in spawned)
            {
                if (item != null)
                {
                    Object.Destroy(item);
                }
            }

            spawned.Clear();
        }

        private IEnumerator FinishDash()
        {
            float end = Time.time + 2f;
            while ((dash.IsDashing || dash.CooldownRemaining > 0f) && Time.time < end)
            {
                yield return null;
            }
        }

        private static bool Emitting(ParticleSystem system) => system != null && system.emission.enabled;

        [UnityTest]
        public IEnumerator MusketDashIsAQuickRegularRoll()
        {
            Assert.IsTrue(inventory.Equip(inventory.GetWeapon<MusketWeapon>().Slot));
            yield return null;

            float startZ = player.transform.position.z;
            Assert.IsTrue(dash.TryDash(Vector3.forward));
            Assert.AreEqual(DashStyle.Regular, dash.Style);

            yield return new WaitForSeconds(dash.Duration * 0.5f);
            Assert.IsTrue(dash.IsDashing);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, look.Body.localRotation), 60f, "the body should be mid roll");

            yield return new WaitForSeconds(dash.Duration * 0.5f + 0.15f);
            Assert.IsFalse(dash.IsDashing);
            float travelled = player.transform.position.z - startZ;
            Assert.AreEqual(dash.MoveFor(DashStyle.Regular).distance, travelled, 1.2f);
            Assert.AreEqual(Quaternion.identity, look.Body.localRotation, "the body should be back upright");
        }

        [UnityTest]
        public IEnumerator BroomDashesAlternateBetweenRidingAndSweeping()
        {
            BroomWeapon broom = inventory.GetWeapon<BroomWeapon>();
            Assert.IsTrue(inventory.Equip(broom.Slot));
            yield return null;
            Transform pivot = broom.SwingPivot;
            float groundY = player.transform.position.y - 1f;

            Assert.IsTrue(dash.TryDash(Vector3.right));
            Assert.AreEqual(DashStyle.BroomRide, dash.Style);
            yield return new WaitForSeconds(dash.Duration * 0.5f);

            Assert.Less(Mathf.Abs(pivot.forward.y), 0.25f, "the riding broom should float level");
            Assert.Greater(Vector3.Dot(pivot.forward, Vector3.left), 0.8f, "its bristles should trail behind");
            Assert.Greater(look.Body.localPosition.y, 0.15f, "the witch should be lifted onto the broom");
            Assert.IsTrue(Emitting(look.Wind), "wind should blow off the back");
            Assert.IsFalse(Emitting(look.Dust));
            Assert.Greater(Vector3.Dot(look.Wind.transform.forward, Vector3.left), 0.9f, "wind should blow backwards");

            yield return FinishDash();
            Assert.IsFalse(Emitting(look.Wind));

            Assert.IsTrue(dash.TryDash(Vector3.right));
            Assert.AreEqual(DashStyle.BroomSweep, dash.Style);
            yield return new WaitForSeconds(dash.Duration * 0.5f);

            Assert.Less(pivot.forward.y, -0.8f, "the sweeping broom should stand upright with bristles down");
            Assert.Less(look.BristleTip.y - groundY, 0.35f, "the bristles should brush the ground");
            Assert.Greater(look.Body.localPosition.y, 0.25f, "the witch should stand on the bristle head");
            Assert.IsTrue(Emitting(look.Dust), "dust should come off the bristles");
            Assert.IsFalse(Emitting(look.Wind));

            yield return FinishDash();
            Assert.IsFalse(Emitting(look.Dust));
            Assert.IsTrue(dash.TryDash(Vector3.right));
            Assert.AreEqual(DashStyle.BroomRide, dash.Style, "back to riding on the third dash");
        }

        [UnityTest]
        public IEnumerator HitsAreIgnoredOnlyAtTheStartOfTheDash()
        {
            inventory.Equip(inventory.GetWeapon<BroomWeapon>().Slot);
            yield return null;

            DamageInfo hit = new DamageInfo { Amount = 5f, Point = player.transform.position, Direction = Vector3.forward };
            Assert.IsTrue(dash.TryDash(Vector3.forward));
            Assert.IsFalse(health.ApplyDamage(hit).Applied, "the opening of the dash should be untouchable");

            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(dash.IsDashing, "still mid dash");
            Assert.IsTrue(health.ApplyDamage(hit).Applied, "later in the dash hits land");
            Assert.IsFalse(dash.IsDashing, "a hit ends the dash");
            Assert.IsFalse(inventory.InputSuspended);
        }

        [UnityTest]
        public IEnumerator DashCancelsASwingAndBlocksAttacksUntilItEnds()
        {
            BroomWeapon broom = inventory.GetWeapon<BroomWeapon>();
            inventory.Equip(broom.Slot);
            yield return null;

            broom.PressLight();
            yield return null;
            Assert.IsTrue(broom.IsBusy);

            Assert.IsTrue(dash.TryDash(Vector3.forward));
            Assert.IsFalse(broom.IsBusy, "the dash should cut the swing short");
            Assert.IsTrue(inventory.InputSuspended);

            yield return new WaitForSeconds(dash.Duration + 0.05f);
            Assert.IsFalse(inventory.InputSuspended);
        }

        [UnityTest]
        public IEnumerator CooldownStopsBackToBackDashes()
        {
            Assert.IsTrue(dash.TryDash(Vector3.forward));
            Assert.IsFalse(dash.TryDash(Vector3.forward), "cannot dash mid dash");

            yield return new WaitForSeconds(dash.Duration + 0.05f);
            Assert.IsFalse(dash.IsDashing);
            Assert.IsFalse(dash.TryDash(Vector3.forward), "still cooling down");

            yield return FinishDash();
            Assert.IsTrue(dash.TryDash(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator ClipsPlayOnlyDuringTheDashAndLeaveTheBroomFreeToSwing()
        {
            Assert.IsTrue(look.UsesClips, "the Player should carry the PlayerDash controller");
            Animator animator = look.Animator;
            Assert.IsFalse(animator.enabled, "the animator stays off between dashes");

            BroomWeapon broom = inventory.GetWeapon<BroomWeapon>();
            inventory.Equip(broom.Slot);
            yield return null;

            // How far a light swing turns the broom before any dash has run
            float baseline = 0f;
            yield return MeasureSwing(broom, value => baseline = value);
            Assert.Greater(baseline, 20f, "a light swing should visibly turn the broom");
            yield return new WaitForSeconds(1f);

            Assert.IsTrue(dash.TryDash(Vector3.forward));
            yield return null;
            Assert.IsTrue(animator.enabled);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(look.StateFor(dash.Style)), "should be playing the ride clip");

            yield return FinishDash();
            Assert.IsFalse(animator.enabled, "switched off again after the dash");

            yield return new WaitForSeconds(0.5f);
            float afterDash = 0f;
            yield return MeasureSwing(broom, value => afterDash = value);
            Assert.AreEqual(baseline, afterDash, baseline * 0.25f, "the swing code should move the broom just as far after a dash");
        }

        // Largest turn away from the starting pose during one light swing
        private static IEnumerator MeasureSwing(BroomWeapon broom, System.Action<float> report)
        {
            Quaternion rest = broom.SwingPivot.localRotation;
            broom.PressLight();
            float largest = 0f;
            float end = Time.time + 0.6f;
            while (Time.time < end)
            {
                yield return null;
                largest = Mathf.Max(largest, Quaternion.Angle(rest, broom.SwingPivot.localRotation));
            }

            report(largest);
        }

        [UnityTest]
        public IEnumerator EachDashStateHasABakedClipThatEndsAtRest()
        {
#if UNITY_EDITOR
            foreach (DashStyle style in new[] { DashStyle.Regular, DashStyle.BroomRide, DashStyle.BroomSweep })
            {
                string name = look.StateFor(style);
                AnimationClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/{name}.anim");
                Assert.IsNotNull(clip, $"{name}.anim is missing");
                Assert.AreEqual(dash.MoveFor(style).duration, clip.length, 0.02f, $"{name} should last as long as its dash");

                UnityEditor.EditorCurveBinding[] bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
                bool movesBroom = System.Array.Exists(bindings, b => b.path.EndsWith("Broom Pivot"));
                Assert.AreEqual(style != DashStyle.Regular, movesBroom, $"only broom dashes should animate the broom ({name})");

                UnityEditor.EditorCurveBinding bodyY = System.Array.Find(bindings, b => b.path == "Body" && b.propertyName == "m_LocalPosition.y");
                AnimationCurve curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, bodyY);
                Assert.AreEqual(0f, curve.Evaluate(clip.length), 0.001f, $"{name} should finish with the body back at rest");
            }
#endif
            yield break;
        }

        [UnityTest]
        public IEnumerator SpaceAndPadADashAndJumpIsUnbound()
        {
            yield return null;
            PlayerInputReader reader = player.GetComponent<PlayerInputReader>();
            InputAction dashAction = Field(reader, "dashAction");
            InputAction jumpAction = Field(reader, "jumpAction");

            List<string> paths = new List<string>();
            foreach (InputBinding binding in dashAction.bindings)
            {
                paths.Add(binding.path);
            }

            CollectionAssert.Contains(paths, "<Keyboard>/space");
            CollectionAssert.Contains(paths, "<Gamepad>/buttonSouth");
            Assert.AreEqual(0, jumpAction.bindings.Count, "Space now dashes instead of jumping");

            Assert.IsFalse(player.GetComponent<MeshRenderer>().enabled, "the root capsule should hand its look to Body");
            Assert.IsTrue(look.Body.GetComponent<MeshRenderer>().enabled);
            Assert.IsNull(look.Body.GetComponent<Collider>(), "Body is visual only");
        }

        private static InputAction Field(PlayerInputReader reader, string name)
        {
            FieldInfo field = typeof(PlayerInputReader).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return (InputAction)field.GetValue(reader);
        }
    }
}
