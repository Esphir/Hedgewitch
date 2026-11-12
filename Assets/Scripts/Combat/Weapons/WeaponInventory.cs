// The weapon belt where slot order matches the number keys
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Combat
{
    // Subscribe here for every hit and defeat from every weapon
    [DisallowMultipleComponent]
    public class WeaponInventory : MonoBehaviour
    {
        [Tooltip("In key order: element 0 is key 1.")]
        [SerializeField] private List<Weapon> weapons = new List<Weapon>();

        [Tooltip("Slot in hand at the start. -1 starts empty-handed.")]
        [SerializeField] private int startingSlot;

        [Header("Wiring (found on this object if left empty)")]
        [Tooltip("A component implementing ICombatInput.")]
        [SerializeField] private MonoBehaviour inputSource;

        [Tooltip("A component implementing ICombatAim.")]
        [SerializeField] private MonoBehaviour aimSource;

        [Tooltip("A component implementing ICombatMotor.")]
        [SerializeField] private MonoBehaviour motorSource;

        private int currentIndex = -1;

        public ICombatInput Input { get; private set; }

        public ICombatAim Aim { get; private set; }

        public ICombatMotor Motor { get; private set; }

        public IReadOnlyList<Weapon> Weapons => weapons;

        public int CurrentIndex => currentIndex;

        public Weapon Current => currentIndex >= 0 && currentIndex < weapons.Count ? weapons[currentIndex] : null;

        // Old weapon then new and either may be null
        public event Action<Weapon, Weapon> WeaponChanged;
        public event Action<Weapon, IDamageable, DamageInfo, DamageResult> HitLanded;
        public event Action<Weapon, IDamageable, DamageInfo> Defeated;

        private void Awake()
        {
            ResolveSources();

            for (int i = 0; i < weapons.Count; i++)
            {
                BindWeapon(weapons[i]);
            }
        }

        private void Start()
        {
            if (currentIndex < 0 && startingSlot >= 0)
            {
                Equip(startingSlot);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                UnbindWeapon(weapons[i]);
            }
        }

        private void Update()
        {
            if (Input == null)
            {
                return;
            }

            CombatInputFrame frame = Input.ReadCombatInput();

            if (frame.SelectSlot >= 0)
            {
                Equip(frame.SelectSlot);
            }
            else if (frame.CycleSlot != 0)
            {
                Cycle(frame.CycleSlot);
            }

            Weapon current = Current;
            if (current != null)
            {
                current.TickInput(frame);
            }
        }

        // Returns false if the slot is empty
        public bool Equip(int index)
        {
            if (index < 0 || index >= weapons.Count || weapons[index] == null)
            {
                return false;
            }

            if (index == currentIndex)
            {
                return true;
            }

            Weapon previous = Current;
            previous?.Unequip();

            currentIndex = index;
            Weapon next = weapons[index];
            next.Equip();

            WeaponChanged?.Invoke(previous, next);
            return true;
        }

        public bool Equip(WeaponSlot slot)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].Slot == slot)
                {
                    return Equip(i);
                }
            }

            return false;
        }

        public void Unequip()
        {
            Weapon previous = Current;
            if (previous == null)
            {
                return;
            }

            previous.Unequip();
            currentIndex = -1;
            WeaponChanged?.Invoke(previous, null);
        }

        public void Cycle(int direction)
        {
            if (weapons.Count == 0)
            {
                return;
            }

            int step = direction >= 0 ? 1 : -1;
            int start = currentIndex < 0 ? (step > 0 ? -1 : 0) : currentIndex;

            for (int i = 1; i <= weapons.Count; i++)
            {
                int index = ((start + step * i) % weapons.Count + weapons.Count) % weapons.Count;
                if (weapons[index] != null)
                {
                    Equip(index);
                    return;
                }
            }
        }

        public T GetWeapon<T>() where T : Weapon
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] is T match)
                {
                    return match;
                }
            }

            return null;
        }

        public Weapon GetWeapon(WeaponSlot slot)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].Slot == slot)
                {
                    return weapons[i];
                }
            }

            return null;
        }

        // Appends unless a slot index is given
        public void AddWeapon(Weapon weapon, int index = -1)
        {
            if (weapon == null || weapons.Contains(weapon))
            {
                return;
            }

            if (index < 0 || index >= weapons.Count)
            {
                weapons.Add(weapon);
            }
            else
            {
                weapons.Insert(index, weapon);
                if (currentIndex >= index)
                {
                    currentIndex++;
                }
            }

            BindWeapon(weapon);
        }

        public void SetSources(ICombatInput input, ICombatAim aim, ICombatMotor motor)
        {
            Input = input;
            Aim = aim;
            Motor = motor;
        }

        private void ResolveSources()
        {
            Input = inputSource as ICombatInput ?? GetComponent<ICombatInput>();
            Aim = aimSource as ICombatAim ?? GetComponent<ICombatAim>();
            Motor = motorSource as ICombatMotor ?? GetComponent<ICombatMotor>();
        }

        private void BindWeapon(Weapon weapon)
        {
            if (weapon == null)
            {
                return;
            }

            weapon.Bind(this);
            weapon.HitLanded += ForwardHit;
            weapon.Defeated += ForwardDefeat;
        }

        private void UnbindWeapon(Weapon weapon)
        {
            if (weapon == null)
            {
                return;
            }

            weapon.HitLanded -= ForwardHit;
            weapon.Defeated -= ForwardDefeat;
        }

        private void ForwardHit(Weapon weapon, IDamageable target, DamageInfo info, DamageResult result)
        {
            HitLanded?.Invoke(weapon, target, info, result);
        }

        private void ForwardDefeat(Weapon weapon, IDamageable target, DamageInfo info)
        {
            Defeated?.Invoke(weapon, target, info);
        }
    }
}
