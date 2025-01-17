﻿using GameSvr.Actor;
using SystemModule;

namespace GameSvr.Monster.Monsters
{
    public class RonObject : MonsterObject
    {
        public RonObject()
            : base()
        {
            SearchTime = M2Share.RandomNumber.Random(1500) + 1500;
        }

        private void AroundAttack()
        {
            short wHitMode = 0;
            GetAttackDir(TargetCret, ref Direction);
            IList<BaseObject> xTargetList = new List<BaseObject>();
            GetMapBaseObjects(Envir, CurrX, CurrY, 1, xTargetList);
            if (xTargetList.Count > 0)
            {
                for (var i = xTargetList.Count - 1; i >= 0; i--)
                {
                    var baseObject = xTargetList[i];
                    if (baseObject != null)
                    {
                        _Attack(ref wHitMode, baseObject);
                        xTargetList.RemoveAt(i);
                    }
                }
            }
            SendRefMsg(Grobal2.RM_HIT, Direction, CurrX, CurrY, 0, "");
        }

        public override void Run()
        {
            if (CanMove())
            {
                if ((HUtil32.GetTickCount() - SearchEnemyTick) > 8000 || (HUtil32.GetTickCount() - SearchEnemyTick) > 1000 && TargetCret == null)
                {
                    SearchEnemyTick = HUtil32.GetTickCount();
                    SearchTarget();
                }
                if (TargetCret != null && Math.Abs(CurrX - TargetCret.CurrX) < 6 && Math.Abs(CurrY - TargetCret.CurrY) < 6 && (HUtil32.GetTickCount() - AttackTick) > NextHitTime)
                {
                    AttackTick = HUtil32.GetTickCount();
                    AroundAttack();
                }
            }
            base.Run();
        }
    }
}