// Project:         RoleplayRealism mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2019 Hazelnut
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Hazelnut

using DaggerfallConnect;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Banking;

namespace RoleplayRealism
{
    public class RoleplayRealism : MonoBehaviour
    {
        public static float EncEffectScaleFactor = 2f;

        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<RoleplayRealism>();
        }

        void Awake()
        {
            ModSettings settings = mod.GetSettings();
            bool bedSleeping = settings.GetBool("Modules", "bedSleeping");
            bool archery = settings.GetBool("Modules", "advancedArchery");
            bool riding = settings.GetBool("Modules", "enhancedRiding");
            bool encumbrance = settings.GetBool("Modules", "encumbranceEffects");
            bool bandaging = settings.GetBool("Modules", "bandaging");
            bool shipPorts = settings.GetBool("Modules", "shipPorts");
            bool rebalance = settings.GetBool("Modules", "itemRebalance");

            InitMod(bedSleeping, archery, riding, encumbrance, bandaging, shipPorts, rebalance);

            mod.IsReady = true;
        }

        public static void InitMod(bool bedSleeping, bool archery, bool riding, bool encumbrance, bool bandaging, bool shipPorts, bool rebalance)
        {
            Debug.Log("Begin mod init: RoleplayRealism");

            if (bedSleeping)
            {
                PlayerActivate.RegisterModelActivation(41000, BedActivation);
                PlayerActivate.RegisterModelActivation(41001, BedActivation);
                PlayerActivate.RegisterModelActivation(41002, BedActivation);
            }

            if (archery)
            {
                // Override adjust to hit and damage formulas
                FormulaHelper.formula_2de_2i.Add("AdjustWeaponHitChanceMod", AdjustWeaponHitChanceMod);
                FormulaHelper.formula_2de_2i.Add("AdjustWeaponAttackDamage", AdjustWeaponAttackDamage);
            }

            if (riding)
            {
                GameObject playerAdvGO = GameObject.Find("PlayerAdvanced");
                if (playerAdvGO)
                {
                    playerAdvGO.AddComponent<EnhancedRiding>();
                }
            }

            if (encumbrance)
            {
                EntityEffectBroker.OnNewMagicRound += EncumbranceEffects_OnNewMagicRound;
            }

            ItemHelper itemHelper = DaggerfallUnity.Instance.ItemHelper;
            if (bandaging)
            {
                itemHelper.RegisterItemUseHander((int)UselessItems2.Bandage, UseBandage);
            }

            if (shipPorts)
            {
                GameManager.Instance.TransportManager.ShipAvailiable = IsShipAvailiable;
            }

            Debug.Log("Finished mod init: RoleplayRealism");
        }

        private static void BedActivation(Transform transform)
        {
            //Debug.Log("zzzzzzzzzz!");
            IUserInterfaceManager uiManager = DaggerfallUI.UIManager;
            uiManager.PushWindow(new DaggerfallRestWindow(uiManager, true));
        }

        private static int AdjustWeaponHitChanceMod(DaggerfallEntity attacker, DaggerfallEntity target, int hitChanceMod, int weaponAnimTime, DaggerfallUnityItem weapon)
        {
            if (weaponAnimTime > 0 && (weapon.TemplateIndex == (int)Weapons.Short_Bow || weapon.TemplateIndex == (int)Weapons.Long_Bow))
            {
                int adjustedHitChanceMod = hitChanceMod;
                if (weaponAnimTime < 200)
                    adjustedHitChanceMod -= 40;
                else if (weaponAnimTime < 500)
                    adjustedHitChanceMod -= 10;
                else if (weaponAnimTime < 1000)
                    adjustedHitChanceMod = hitChanceMod;
                else if (weaponAnimTime < 2000)
                    adjustedHitChanceMod += 10;
                else if (weaponAnimTime > 5000)
                    adjustedHitChanceMod -= 10;
                else if (weaponAnimTime > 8000)
                    adjustedHitChanceMod -= 20;

                Debug.LogFormat("Adjusted Weapon HitChanceMod for bow drawing from {0} to {1} (t={2}ms)", hitChanceMod, adjustedHitChanceMod, weaponAnimTime);
                return adjustedHitChanceMod;
            }

            return hitChanceMod;
        }

        private static int AdjustWeaponAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, int damage, int weaponAnimTime, DaggerfallUnityItem weapon)
        {
            if (weaponAnimTime > 0 && (weapon.TemplateIndex == (int)Weapons.Short_Bow || weapon.TemplateIndex == (int)Weapons.Long_Bow))
            {
                double adjustedDamage = damage;
                if (weaponAnimTime < 800)
                    adjustedDamage *= (double)weaponAnimTime / 800;
                else if (weaponAnimTime < 5000)
                    adjustedDamage = damage;
                else if (weaponAnimTime < 6000)
                    adjustedDamage *= 0.85;
                else if (weaponAnimTime < 8000)
                    adjustedDamage *= 0.75;
                else if (weaponAnimTime < 9000)
                    adjustedDamage *= 0.5;
                else if (weaponAnimTime >= 9000)
                    adjustedDamage *= 0.25;

                Debug.LogFormat("Adjusted Weapon Damage for bow drawing from {0} to {1} (t={2}ms)", damage, (int)adjustedDamage, weaponAnimTime);
                return (int)adjustedDamage;
            }

            return damage;
        }

        private static void EncumbranceEffects_OnNewMagicRound()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            if (playerEntity.CurrentHealth > 0 && playerEntity.EntityBehaviour.enabled && !playerEntity.IsResting &&
                !GameManager.Instance.EntityEffectBroker.SyntheticTimeIncrease)
            {
                float encPc = playerEntity.CarriedWeight / playerEntity.MaxEncumbrance;
                float encOver = Mathf.Max(encPc - 0.75f, 0f) * EncEffectScaleFactor;
                if (encOver > 0 && encOver < 0.8)
                {
                    int speedEffect = (int)(playerEntity.Stats.PermanentSpeed * encOver);
                    int fatigueEffect = (int)(encOver * 100);
                    //Debug.LogFormat("Encumbrance {0}, over {1} = effects: {2} speed, {3} fatigue", encPc, encOver, speedEffect, fatigueEffect);

                    playerEntity.DecreaseFatigue(fatigueEffect, false);

                    EntityEffectManager playerEffectManager = playerEntity.EntityBehaviour.GetComponent<EntityEffectManager>();
                    int[] statMods = new int[DaggerfallStats.Count];
                    statMods[(int)DFCareer.Stats.Speed] = -speedEffect;
                    playerEffectManager.MergeDirectStatMods(statMods);
                }
            }
        }

        private static bool UseBandage(DaggerfallUnityItem item, ItemCollection collection)
        {
            if (collection != null)
            {
                PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                int medical = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Medical);
                int heal = (int) Mathf.Min(medical / 2, playerEntity.MaxHealth * 0.4f);
                Debug.LogFormat("Applying a Bandage to heal {0} health.", heal);
                collection.RemoveItem(item);

                playerEntity.IncreaseHealth(heal);
            }
            return true;
        }

        private static bool IsShipAvailiable()
        {
            if (GameManager.Instance.TransportManager.IsOnShip())
                return true;

            DFLocation location = GameManager.Instance.PlayerGPS.CurrentLocation;
            if (location.Loaded == true)
            {
                return location.Exterior.ExteriorData.PortTownAndUnknown != 0 && DaggerfallBankManager.OwnsShip;
            }

            return false;
        }
    }
}