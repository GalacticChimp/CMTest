﻿using System;
using System.Collections.Concurrent;

using Iam.Scripts.Helpers.Battle.Resolutions;
using Iam.Scripts.Models.Equippables;
using Iam.Scripts.Models.Soldiers;

namespace Iam.Scripts.Helpers.Battle.Actions
{
    class ShootAction : IAction
    {
        private readonly BattleSoldier _soldier;
        private readonly RangedWeapon _weapon;
        private readonly BattleSoldier _target;
        private readonly float _range;
        private readonly int _numberOfShots;
        private readonly bool _useBulk;
        private readonly ConcurrentBag<WoundResolution> _resultList;

        public ShootAction(BattleSoldier shooter, RangedWeapon weapon, BattleSoldier target, float range, int numberOfShots, bool useBulk, ConcurrentBag<WoundResolution> resultList)
        {
            _soldier = shooter;
            _weapon = weapon;
            _target = target;
            _range = range;
            _numberOfShots = numberOfShots;
            _useBulk = useBulk;
            _resultList = resultList;
        }

        public void Execute()
        {
            float modifier = CalculateToHitModifiers();
            float skill = BattleHelpers.GetWeaponSkillPlusStat(_soldier.Soldier, _weapon.Template);
            float roll = 10.5f + (3.0f * (float)Gaussian.NextGaussianDouble());
            float total = roll + skill + modifier;
            if(total > 0)
            {
                // there were hits, determine how many
                do
                {
                    HandleHit();
                    total -= _weapon.Template.Recoil;
                } while (total > 1);
            }
        }

        private float CalculateToHitModifiers()
        {
            float totalModifier = 0;
            if (_useBulk)
            {
                totalModifier -= _weapon.Template.Bulk;
            }
            if(_soldier.Aim != null && _soldier.Aim.Item1 == _target && _soldier.Aim.Item2 == _weapon)
            {
                totalModifier += _soldier.Aim.Item3 + _weapon.Template.Accuracy + 1;
            }
            totalModifier += BattleHelpers.CalculateRateOfFireModifier(_numberOfShots);
            totalModifier += BattleHelpers.CalculateSizeModifier(_target.Soldier.Size);
            totalModifier += BattleHelpers.CalculateRangeModifier(_range);

            return totalModifier;
        }
        
        private void HandleHit()
        {
            HitLocation hitLocation = DetermineHitLocation(_target.Soldier);
            // make sure this body part hasn't already been shot off
            if(!hitLocation.IsSevered)
            {
                float damage = BattleHelpers.CalculateDamageAtRange(_weapon, _range) * (3.5f + ((float)Gaussian.NextGaussianDouble() * 1.75f));
                float effectiveArmor = _target.Armor.Template.ArmorProvided * _weapon.Template.ArmorMultiplier;
                float penDamage = damage - effectiveArmor;
                if (penDamage > 0)
                {
                    float totalDamage = penDamage * _weapon.Template.PenetrationMultiplier;
                    _resultList.Add(new WoundResolution(_target, totalDamage, hitLocation));
                }
            }
        }

        private HitLocation DetermineHitLocation(Soldier soldier)
        {
            // we're using the "lottery ball" approach to randomness here, where each point of probability
            // for each available body party defines the size of the random linear distribution
            // TODO: factor in cover/body position
            // 
            int roll = UnityEngine.Random.Range(1, soldier.Body.TotalProbability);
            foreach (HitLocation location in soldier.Body.HitLocations)
            {
                if (roll < location.Template.HitProbability)
                {
                    return location;
                }
                else
                {
                    // this is basically an easy iterative way to figure out which body part on the "chart" the roll matches
                    roll -= location.Template.HitProbability;
                }
            }
            // this should never happen
            throw new InvalidOperationException("Could not determine a hit location");
        }
    }
}
