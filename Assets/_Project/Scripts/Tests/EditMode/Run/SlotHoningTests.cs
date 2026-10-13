using System;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.EditModeTests
{
    // Honing belongs to a character's slot (docs/PLAN_ITEMS.md 1.5): +1 per
    // hone, at most +10, separate per hand and per member, and +0 where no
    // entry exists.
    public class SlotHoningTests
    {
        [Test]
        public void AnUntouchedSlot_ReadsPlusZero()
        {
            var honing = new List<SlotHoningEntry>();

            Assert.AreEqual(0, SlotHoning.PlusOf(honing, "shawn", EquipmentSlot.Torso));
            Assert.AreEqual(0, SlotHoning.PlusOf(null, "shawn", EquipmentSlot.Torso));
        }

        [Test]
        public void Hone_AddsOne_AndKeepsOneEntryPerSlot()
        {
            var honing = new List<SlotHoningEntry>();

            Assert.AreEqual(1, SlotHoning.Hone(honing, "shawn", EquipmentSlot.Torso));
            Assert.AreEqual(2, SlotHoning.Hone(honing, "shawn", EquipmentSlot.Torso));

            Assert.AreEqual(2, SlotHoning.PlusOf(honing, "shawn", EquipmentSlot.Torso));
            Assert.AreEqual(1, honing.Count);
        }

        [Test]
        public void Hone_StopsAtTen()
        {
            var honing = new List<SlotHoningEntry>();
            for (int i = 0; i < 12; i++)
            {
                SlotHoning.Hone(honing, "shawn", EquipmentSlot.Head);
            }

            Assert.AreEqual(10, SlotHoning.PlusOf(honing, "shawn", EquipmentSlot.Head));
            Assert.IsFalse(SlotHoning.CanHone(honing, "shawn", EquipmentSlot.Head));
            Assert.IsTrue(SlotHoning.CanHone(honing, "shawn", EquipmentSlot.Legs));
        }

        // A sword moved between hands reads the new hand's honing.
        [Test]
        public void TheTwoHands_AreSeparateSlots()
        {
            var honing = new List<SlotHoningEntry>();
            SlotHoning.Hone(honing, "shawn", EquipmentSlot.Weapon1);
            SlotHoning.Hone(honing, "shawn", EquipmentSlot.Weapon1);

            Assert.AreEqual(2, SlotHoning.PlusOf(honing, "shawn", EquipmentSlot.Weapon1));
            Assert.AreEqual(0, SlotHoning.PlusOf(honing, "shawn", EquipmentSlot.Weapon2));
        }

        [Test]
        public void Members_AreSeparate()
        {
            var honing = new List<SlotHoningEntry>();
            SlotHoning.Hone(honing, "odette", EquipmentSlot.Shoes);

            Assert.AreEqual(1, SlotHoning.PlusOf(honing, "odette", EquipmentSlot.Shoes));
            Assert.AreEqual(0, SlotHoning.PlusOf(honing, "bjorn", EquipmentSlot.Shoes));
        }

        [Test]
        public void HoningASlotWithNoOwner_Throws()
        {
            Assert.Throws<ArgumentException>(() => SlotHoning.Hone(new List<SlotHoningEntry>(), "", EquipmentSlot.Head));
        }
    }
}
