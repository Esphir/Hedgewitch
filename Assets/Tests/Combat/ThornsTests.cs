// Play mode tests for thorns biting the player pushing them away and repeating while they stay in
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hedgewitch.Combat.Tests
{
    public class ThornsTests
    {
        private readonly List<Object> spawned = new List<Object>();

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

        private ThornsHazard MakeThorns(bool trigger, bool hurtEnemies = false, float interval = 0.3f)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            spawned.Add(floor);

            GameObject thorns = new GameObject("Thorns");
            spawned.Add(thorns);
            BoxCollider box = thorns.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 1.2f, 2f);
            box.center = new Vector3(0f, 0.6f, 0f);
            box.isTrigger = trigger;
            ThornsHazard hazard = thorns.AddComponent<ThornsHazard>();
            hazard.EditorConfigure(5f, 4.5f, interval, hurtEnemies);
            return hazard;
        }

        private Health MakeVictim(Vector3 position, string tag = "Player")
        {
            GameObject victim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spawned.Add(victim);
            victim.tag = tag;
            victim.transform.position = position;
            Rigidbody body = victim.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            Health health = victim.AddComponent<Health>();
            victim.AddComponent<HitReaction>();
            return health;
        }

        private static IEnumerator Physics(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator WalkingIntoATriggerPatchHurtsAndPushesOut()
        {
            MakeThorns(true);
            Health player = MakeVictim(new Vector3(0.8f, 1f, 0f));

            yield return Physics(3);

            Assert.AreEqual(player.Max - 5f, player.Current, 1e-3f);
            Assert.Greater(player.GetComponent<Rigidbody>().linearVelocity.x, 1f, "should be pushed away from the thorns");
        }

        [UnityTest]
        public IEnumerator StayingInsideBitesAgainAfterTheInterval()
        {
            MakeThorns(true, interval: 0.3f);
            Health player = MakeVictim(new Vector3(0.5f, 1f, 0f));
            player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;

            yield return Physics(3);
            Assert.AreEqual(player.Max - 5f, player.Current, 1e-3f);

            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(player.Max - 5f, player.Current, 1e-3f, "bit again before the interval");

            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(player.Max - 10f, player.Current, 1e-3f);
        }

        [UnityTest]
        public IEnumerator BumpingASolidHedgeHurtsAndPushesBack()
        {
            MakeThorns(false);
            Health player = MakeVictim(new Vector3(-3f, 1f, 0f));
            Rigidbody body = player.GetComponent<Rigidbody>();

            bool bitten = false;
            for (int i = 0; i < 60 && !bitten; i++)
            {
                body.linearVelocity = new Vector3(6f, body.linearVelocity.y, 0f);
                yield return new WaitForFixedUpdate();
                bitten = player.Current < player.Max;
            }

            Assert.IsTrue(bitten, "touching the hedge never hurt");
            yield return new WaitForFixedUpdate();
            Assert.Less(body.linearVelocity.x, 0f, "should bounce back off the hedge");
        }

        [UnityTest]
        public IEnumerator EnemiesAreSafeUnlessTheOptionIsOn()
        {
            MakeThorns(true);
            Health enemy = MakeVictim(new Vector3(0.8f, 1f, 0f), "Untagged");
            yield return Physics(4);
            Assert.AreEqual(enemy.Max, enemy.Current);

            MakeThorns(true, hurtEnemies: true).transform.position = new Vector3(6f, 0f, 0f);
            Health other = MakeVictim(new Vector3(6.8f, 1f, 0f), "Untagged");
            yield return Physics(4);
            Assert.Less(other.Current, other.Max);
        }
    }
}
