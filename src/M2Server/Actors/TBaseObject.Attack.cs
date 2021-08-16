﻿using SystemModule;

namespace M2Server
{
    public partial class TBaseObject
    {
        protected void SendAttackMsg(short wIdent, byte btDir, int nX, int nY)
        {
            SendRefMsg(wIdent, btDir, nX, nY, 0, "");
        }
        
        /// <summary>
        /// 武器升级设置
        /// </summary>
        /// <param name="UserItem"></param>
        public void AttackDir_CheckWeaponUpgradeStatus(TUserItem UserItem)
        {
            if ((UserItem.btValue[0] + UserItem.btValue[1] + UserItem.btValue[2]) < M2Share.g_Config.nUpgradeWeaponMaxPoint)
            {
                if (UserItem.btValue[10] == 1)
                {
                    UserItem.wIndex = 0;
                }
                if (HUtil32.RangeInDefined(UserItem.btValue[10], 10, 13))
                {
                    UserItem.btValue[0] = (byte)(UserItem.btValue[0] + UserItem.btValue[10] - 9);
                }
                if (HUtil32.RangeInDefined(UserItem.btValue[10], 20, 23))
                {
                    UserItem.btValue[1] = (byte)(UserItem.btValue[1] + UserItem.btValue[10] - 19);
                }
                if (HUtil32.RangeInDefined(UserItem.btValue[10], 30, 33))
                {
                    UserItem.btValue[2] = (byte)(UserItem.btValue[2] + UserItem.btValue[10] - 29);
                }
            }
            else
            {
                UserItem.wIndex = 0;
            }
            UserItem.btValue[10] = 0;
        }

        public void AttackDir_CheckWeaponUpgrade()
        {
            TUserItem UseItems;
            TPlayObject PlayObject;
            MirItem StdItem;
            if (m_UseItems[Grobal2.U_WEAPON].btValue[10] > 0)
            {
                UseItems = m_UseItems[Grobal2.U_WEAPON];
                AttackDir_CheckWeaponUpgradeStatus(m_UseItems[Grobal2.U_WEAPON]);
                if (m_UseItems[Grobal2.U_WEAPON].wIndex == 0)
                {
                    SysMsg(M2Share.g_sTheWeaponBroke, TMsgColor.c_Red, TMsgType.t_Hint);
                    PlayObject = this as TPlayObject;
                    PlayObject.SendDelItems(UseItems);
                    // PlayObject.StatusChanged;
                    SendRefMsg(Grobal2.RM_BREAKWEAPON, 0, 0, 0, 0, "");
                    StdItem = M2Share.UserEngine.GetStdItem(UseItems.wIndex);
                    if (StdItem.NeedIdentify == 1)
                    {
                        // UserEngine.GetStdItemName(UseItems.wIndex) + #9 +
                        M2Share.AddGameDataLog("21" + "\t" + m_sMapName + "\t" + m_nCurrX.ToString() + "\t" + m_nCurrY.ToString() + "\t" + m_sCharName + "\t" + StdItem.Name + "\t" + UseItems.MakeIndex.ToString() + "\t" + '1' + "\t" + '0');
                    }
                    FeatureChanged();
                }
                else
                {
                    SysMsg(M2Share.sTheWeaponRefineSuccessfull, TMsgColor.c_Red, TMsgType.t_Hint);
                    PlayObject = this as TPlayObject;
                    PlayObject.SendUpdateItem(m_UseItems[Grobal2.U_WEAPON]);
                    StdItem = M2Share.UserEngine.GetStdItem(UseItems.wIndex);
                    if (StdItem.NeedIdentify == 1)
                    {
                        // UserEngine.GetStdItemName(UseItems.wIndex) + #9 +
                        M2Share.AddGameDataLog("20" + "\t" + m_sMapName + "\t" + m_nCurrX.ToString() + "\t" + m_nCurrY.ToString() + "\t" + m_sCharName + "\t" + StdItem.Name + "\t" + UseItems.MakeIndex.ToString() + "\t" + '1' + "\t" + '0');
                    }
                    RecalcAbilitys();
                    SendMsg(this, Grobal2.RM_ABILITY, 0, 0, 0, 0, "");
                    SendMsg(this, Grobal2.RM_SUBABILITY, 0, 0, 0, 0, "");
                }
            }
        }

        public virtual void AttackDir(TBaseObject TargeTBaseObject, short wHitMode, byte nDir)
        {
            TBaseObject AttackTarget;
            bool boPowerHit;
            bool boFireHit;
            bool boCrsHit;
            bool bo41;
            bool boTwinHit;
            bool bo43;
            short wIdent;
            const string sExceptionMsg = "[Exception] TBaseObject::AttackDir";
            try
            {
                if ((wHitMode == 5) && (m_MagicBanwolSkill != null)) // 半月
                {
                    if (m_WAbil.MP > 0)
                    {
                        DamageSpell((ushort)(m_MagicBanwolSkill.MagicInfo.btDefSpell + GetMagicSpell(m_MagicBanwolSkill)));
                        HealthSpellChanged();
                    }
                    else
                    {
                        wHitMode = Grobal2.RM_HIT;
                    }
                }
                if ((wHitMode == 12) && (m_MagicRedBanwolSkill != null))
                {
                    if (m_WAbil.MP > 0)
                    {
                        DamageSpell((ushort)(m_MagicRedBanwolSkill.MagicInfo.btDefSpell + GetMagicSpell(m_MagicRedBanwolSkill)));
                        HealthSpellChanged();
                    }
                    else
                    {
                        wHitMode = Grobal2.RM_HIT;
                    }
                }
                if ((wHitMode == 8) && (m_MagicCrsSkill != null))
                {
                    if (m_WAbil.MP > 0)
                    {
                        DamageSpell((ushort)(m_MagicCrsSkill.MagicInfo.btDefSpell + GetMagicSpell(m_MagicCrsSkill)));
                        HealthSpellChanged();
                    }
                    else
                    {
                        wHitMode = Grobal2.RM_HIT;
                    }
                }

                m_btDirection = nDir;
                if (TargeTBaseObject == null)
                {
                    AttackTarget = GetPoseCreate();
                }
                else
                {
                    AttackTarget = TargeTBaseObject;
                }
                if ((AttackTarget != null) && (m_UseItems[Grobal2.U_WEAPON] != null) && (m_UseItems[Grobal2.U_WEAPON].wIndex > 0))
                {
                    AttackDir_CheckWeaponUpgrade();
                }

                boPowerHit = m_boPowerHit;
                boFireHit = m_boFireHitSkill;
                boCrsHit = m_boCrsHitkill;
                bo41 = m_bo41kill;
                boTwinHit = m_boTwinHitSkill;
                bo43 = m_bo43kill;
                if (_Attack(ref wHitMode, AttackTarget))
                {
                    SetTargetCreat(AttackTarget);
                }
                wIdent = Grobal2.RM_HIT;
                if (m_btRaceServer == Grobal2.RC_PLAYOBJECT)
                {
                    switch (wHitMode)
                    {
                        case 0:
                            wIdent = Grobal2.RM_HIT;
                            break;
                        case 1:
                            wIdent = Grobal2.RM_HEAVYHIT;
                            break;
                        case 2:
                            wIdent = Grobal2.RM_BIGHIT;
                            break;
                        case 3:
                            if (boPowerHit)
                            {
                                wIdent = Grobal2.RM_SPELL2;
                            }
                            break;
                        case 4:
                            if (m_MagicErgumSkill != null)
                            {
                                wIdent = Grobal2.RM_LONGHIT;
                            }
                            break;
                        case 5:
                            if (m_MagicBanwolSkill != null)
                            {
                                wIdent = Grobal2.RM_WIDEHIT;
                            }
                            break;
                        case 7:
                            if (boFireHit)
                            {
                                wIdent = Grobal2.RM_FIREHIT;
                            }
                            break;
                        case 8:
                            if (m_MagicCrsSkill != null)
                            {
                                wIdent = Grobal2.RM_CRSHIT;
                            }
                            break;
                        case 9:
                            if (boTwinHit)
                            {
                                wIdent = Grobal2.RM_TWINHIT;
                            }
                            break;
                        case 12:
                            if (m_MagicRedBanwolSkill != null)
                            {
                                wIdent = Grobal2.RM_WIDEHIT;
                            }
                            break;
                    }
                }
                SendAttackMsg(wIdent, m_btDirection, m_nCurrX, m_nCurrY);
            }
            catch (Exception e)
            {
                M2Share.ErrorMessage(sExceptionMsg);
                M2Share.ErrorMessage(e.Message);
            }
        }
        
        // 攻击角色
        public bool _Attack_DirectAttack(TBaseObject BaseObject, int nSecPwr)
        {
            bool result = false;
            if ((m_btRaceServer == Grobal2.RC_PLAYOBJECT) || (BaseObject.m_btRaceServer == Grobal2.RC_PLAYOBJECT) || !(InSafeZone() && BaseObject.InSafeZone()))
            {
                if (IsProperTarget(BaseObject))
                {
                    if (M2Share.RandomNumber.Random(BaseObject.m_btSpeedPoint) < m_btHitPoint)
                    {
                        BaseObject.StruckDamage(nSecPwr);
                        BaseObject.SendDelayMsg(Grobal2.RM_STRUCK, Grobal2.RM_10101, (short)nSecPwr, BaseObject.m_WAbil.HP, BaseObject.m_WAbil.MaxHP, ObjectId, "", 500);
                        if (BaseObject.m_btRaceServer != Grobal2.RC_PLAYOBJECT)
                        {
                            BaseObject.SendMsg(BaseObject, Grobal2.RM_STRUCK, (short)nSecPwr, BaseObject.m_WAbil.HP, BaseObject.m_WAbil.MaxHP, ObjectId, "");
                        }
                        result = true;
                    }
                }
            }
            return result;
        }

        // 刺杀前面一个位置的攻击
        public bool _Attack_SwordLongAttack(int nSecPwr)
        {
            bool result = false;
            short nX = 0;
            short nY = 0;
            nSecPwr = HUtil32.Round(nSecPwr * M2Share.g_Config.nSwordLongPowerRate / 100);
            if (m_PEnvir.GetNextPosition(m_nCurrX, m_nCurrY, m_btDirection, 2, ref nX, ref nY))
            {
                TBaseObject BaseObject = (TBaseObject)m_PEnvir.GetMovingObject(nX, nY, true);
                if (BaseObject != null)
                {
                    if ((nSecPwr > 0) && IsProperTarget(BaseObject))
                    {
                        result = _Attack_DirectAttack(BaseObject, nSecPwr);
                        SetTargetCreat(BaseObject);
                    }
                    result = true;
                }
            }
            return result;
        }

        // 半月攻击
        public bool _Attack_SwordWideAttack(int nSecPwr)
        {
            bool result = false;
            int nC = 0;
            int n10 = 0;
            short nX = 0;
            short nY = 0;
            TBaseObject BaseObject;
            while (true)
            {
                n10 = (m_btDirection + M2Share.g_Config.WideAttack[nC]) % 8;
                if (m_PEnvir.GetNextPosition(m_nCurrX, m_nCurrY, n10, 1, ref nX, ref nY))
                {
                    BaseObject = (TBaseObject)m_PEnvir.GetMovingObject(nX, nY, true);
                    if ((nSecPwr > 0) && (BaseObject != null) && IsProperTarget(BaseObject))
                    {
                        result = _Attack_DirectAttack(BaseObject, nSecPwr);
                        SetTargetCreat(BaseObject);
                    }
                }
                nC++;
                if (nC >= 3)
                {
                    break;
                }
            }
            return result;
        }

        public bool _Attack_CrsWideAttack(int nSecPwr)
        {
            bool result = false;
            int nC = 0;
            int n10 = 0;
            short nX = 0;
            short nY = 0;
            TBaseObject BaseObject;
            while (true)
            {
                n10 = (m_btDirection + M2Share.g_Config.CrsAttack[nC]) % 8;
                if (m_PEnvir.GetNextPosition(m_nCurrX, m_nCurrY, n10, 1, ref nX, ref nY))
                {
                    BaseObject = (TBaseObject)m_PEnvir.GetMovingObject(nX, nY, true);
                    if ((nSecPwr > 0) && (BaseObject != null) && IsProperTarget(BaseObject))
                    {
                        result = _Attack_DirectAttack(BaseObject, nSecPwr);
                        SetTargetCreat(BaseObject);
                    }
                }
                nC++;
                if (nC >= 7)
                {
                    break;
                }
            }
            return result;
        }

        public void _Attack_sub_4C1E5C_sub_4C1DC0(ref TBaseObject BaseObject, byte btDir, ref short nX, ref short nY, int nSecPwr)
        {
            if (m_PEnvir.GetNextPosition(m_nCurrX, m_nCurrY, btDir, 1, ref nX, ref nY))
            {
                BaseObject = (TBaseObject)m_PEnvir.GetMovingObject(nX, nY, true);
                if ((nSecPwr > 0) && (BaseObject != null))
                {
                    _Attack_DirectAttack(BaseObject, nSecPwr);
                }
            }
        }

        public void _Attack_sub_4C1E5C(int nSecPwr)
        {
            byte btDir = 0;
            short nX = 0;
            short nY = 0;
            TBaseObject BaseObject = null;
            btDir = m_btDirection;
            m_PEnvir.GetNextPosition(m_nCurrX, m_nCurrY, btDir, 1, ref nX, ref nY);
            _Attack_sub_4C1E5C_sub_4C1DC0(ref BaseObject, btDir, ref nX, ref nY, nSecPwr);
            btDir = M2Share.sub_4B2F80(m_btDirection, 2);
            _Attack_sub_4C1E5C_sub_4C1DC0(ref BaseObject, btDir, ref nX, ref nY, nSecPwr);
            btDir = M2Share.sub_4B2F80(m_btDirection, 6);
            _Attack_sub_4C1E5C_sub_4C1DC0(ref BaseObject, btDir, ref nX, ref nY, nSecPwr);
        }

        public bool _Attack(ref short wHitMode, TBaseObject AttackTarget)
        {
            int n20;
            bool result = false;
            try
            {
                bool bo21 = false;
                int nWeaponDamage = 0;
                int nPower = 0;
                int nSecPwr = 0;
                if (AttackTarget != null)
                {
                    nPower = GetAttackPower(HUtil32.LoWord(m_WAbil.DC), HUtil32.HiWord(m_WAbil.DC) - HUtil32.LoWord(m_WAbil.DC));
                    if ((wHitMode == 3) && m_boPowerHit)
                    {
                        m_boPowerHit = false;
                        nPower += m_nHitPlus;
                        bo21 = true;
                    }
                    if ((wHitMode == 7) && m_boFireHitSkill) // 烈火剑法
                    {
                        m_boFireHitSkill = false;
                        m_dwLatestFireHitTick = HUtil32.GetTickCount();// Jacky 禁止双烈火
                        nPower = nPower + HUtil32.Round(nPower / 100 * (m_nHitDouble * 10));
                        bo21 = true;
                    }
                    if ((wHitMode == 9) && m_boTwinHitSkill) // 烈火剑法
                    {
                        m_boTwinHitSkill = false;
                        m_dwLatestTwinHitTick = HUtil32.GetTickCount();// Jacky 禁止双烈火
                        nPower = nPower + HUtil32.Round(nPower / 100 * (m_nHitDouble * 10));
                        bo21 = true;
                    }
                }
                else
                {
                    nPower = GetAttackPower(HUtil32.LoWord(m_WAbil.DC), HUtil32.HiWord(m_WAbil.DC) - HUtil32.LoWord(m_WAbil.DC));
                    if ((wHitMode == 3) && m_boPowerHit)
                    {
                        m_boPowerHit = false;
                        nPower += m_nHitPlus;
                        bo21 = true;
                    }
                    // Jacky 防止砍空刀刀烈火
                    if ((wHitMode == 7) && m_boFireHitSkill)
                    {
                        m_boFireHitSkill = false;
                        m_dwLatestFireHitTick = HUtil32.GetTickCount();// Jacky 禁止双烈火
                    }
                    if ((wHitMode == 9) && m_boTwinHitSkill)
                    {
                        m_boTwinHitSkill = false;
                        m_dwLatestTwinHitTick = HUtil32.GetTickCount();// Jacky 禁止双烈火
                    }
                }
                if (wHitMode == 4)
                {
                    // 刺杀
                    nSecPwr = 0;
                    if (m_btRaceServer == Grobal2.RC_PLAYOBJECT)
                    {
                        if (m_MagicErgumSkill != null)
                        {
                            nSecPwr = HUtil32.Round(nPower / (m_MagicErgumSkill.MagicInfo.btTrainLv + 2) * (m_MagicErgumSkill.btLevel + 2));
                        }
                    }
                    if (nSecPwr > 0)
                    {
                        if (!_Attack_SwordLongAttack(nSecPwr) && M2Share.g_Config.boLimitSwordLong)
                        {
                            wHitMode = 0;
                        }
                    }
                }
                if (wHitMode == 5)
                {
                    nSecPwr = 0;
                    if (m_btRaceServer == Grobal2.RC_PLAYOBJECT)
                    {
                        if (m_MagicBanwolSkill != null)
                        {
                            nSecPwr = HUtil32.Round(nPower / (m_MagicBanwolSkill.MagicInfo.btTrainLv + 10) * (m_MagicBanwolSkill.btLevel + 2));
                        }
                    }
                    if (nSecPwr > 0)
                    {
                        _Attack_SwordWideAttack(nSecPwr);
                    }
                }
                if (wHitMode == 12)
                {
                    nSecPwr = 0;
                    if (m_btRaceServer == Grobal2.RC_PLAYOBJECT)
                    {
                        if (m_MagicRedBanwolSkill != null)
                        {
                            nSecPwr = HUtil32.Round(nPower / (m_MagicRedBanwolSkill.MagicInfo.btTrainLv + 10) * (m_MagicRedBanwolSkill.btLevel + 2));
                        }
                    }
                    if (nSecPwr > 0)
                    {
                        _Attack_SwordWideAttack(nSecPwr);
                    }
                }

                if (wHitMode == 6)
                {
                    nSecPwr = 0;
                    if (nSecPwr > 0)
                    {
                        _Attack_sub_4C1E5C(nSecPwr);
                    }
                }
                if (wHitMode == 8)
                {
                    nSecPwr = 0;
                    if (m_btRaceServer == Grobal2.RC_PLAYOBJECT)
                    {
                        if (m_MagicCrsSkill != null)
                        {
                            nSecPwr = HUtil32.Round(nPower / (m_MagicCrsSkill.MagicInfo.btTrainLv + 10) * (m_MagicCrsSkill.btLevel + 2));
                        }
                    }
                    if (nSecPwr > 0)
                    {
                        _Attack_CrsWideAttack(nSecPwr);
                    }
                }
                if (AttackTarget == null)
                {
                    return result;
                }
                if (IsProperTarget(AttackTarget))
                {
                    if (AttackTarget.m_btHitPoint > 0)
                    {
                        if (m_btHitPoint < M2Share.RandomNumber.Random(AttackTarget.m_btSpeedPoint))
                        {
                            nPower = 0;
                        }
                    }
                }
                else
                {
                    nPower = 0;
                }
                if (nPower > 0)
                {
                    nPower = AttackTarget.GetHitStruckDamage(this, nPower);
                    nWeaponDamage = M2Share.RandomNumber.Random(5) + 2 - m_AddAbil.btWeaponStrong;
                }
                if (nPower > 0)
                {
                    AttackTarget.StruckDamage(nPower);
                    AttackTarget.SendDelayMsg(Grobal2.RM_STRUCK, Grobal2.RM_10101, nPower, AttackTarget.m_WAbil.HP, AttackTarget.m_WAbil.MaxHP, ObjectId, "", 200);
                    if (!AttackTarget.m_boUnParalysis && m_boParalysis && (M2Share.RandomNumber.Random(AttackTarget.m_btAntiPoison + M2Share.g_Config.nAttackPosionRate) == 0))
                    {
                        AttackTarget.MakePosion(Grobal2.POISON_STONE, M2Share.g_Config.nAttackPosionTime, 0);
                    }
                    // 虹魔，吸血
                    if (m_nHongMoSuite > 0)
                    {
                        m_db3B0 = nPower / 100 * m_nHongMoSuite;
                        if (m_db3B0 >= 2.0)
                        {
                            n20 = Convert.ToInt32(m_db3B0);
                            m_db3B0 = n20;
                            DamageHealth(-n20);
                        }
                    }
                    if ((m_MagicOneSwordSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicOneSwordSkill.btLevel < 3) && (m_MagicOneSwordSkill.MagicInfo.TrainLevel[m_MagicOneSwordSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicOneSwordSkill, M2Share.RandomNumber.Random(3) + 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicOneSwordSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicOneSwordSkill.MagicInfo.wMagicID, m_MagicOneSwordSkill.btLevel, m_MagicOneSwordSkill.nTranPoint, "", 3000);
                        }
                    }
                    if (bo21 && (m_MagicPowerHitSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicPowerHitSkill.btLevel < 3) && (m_MagicPowerHitSkill.MagicInfo.TrainLevel[m_MagicPowerHitSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicPowerHitSkill, M2Share.RandomNumber.Random(3) + 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicPowerHitSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicPowerHitSkill.MagicInfo.wMagicID, m_MagicPowerHitSkill.btLevel, m_MagicPowerHitSkill.nTranPoint, "", 3000);
                        }
                    }
                    if ((wHitMode == 4) && (m_MagicErgumSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicErgumSkill.btLevel < 3) && (m_MagicErgumSkill.MagicInfo.TrainLevel[m_MagicErgumSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicErgumSkill, 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicErgumSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicErgumSkill.MagicInfo.wMagicID, m_MagicErgumSkill.btLevel, m_MagicErgumSkill.nTranPoint, "", 3000);
                        }
                    }
                    if ((wHitMode == 5) && (m_MagicBanwolSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicBanwolSkill.btLevel < 3) && (m_MagicBanwolSkill.MagicInfo.TrainLevel[m_MagicBanwolSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicBanwolSkill, 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicBanwolSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicBanwolSkill.MagicInfo.wMagicID, m_MagicBanwolSkill.btLevel, m_MagicBanwolSkill.nTranPoint, "", 3000);
                        }
                    }
                    if ((wHitMode == 12) && (m_MagicRedBanwolSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicRedBanwolSkill.btLevel < 3) && (m_MagicRedBanwolSkill.MagicInfo.TrainLevel[m_MagicRedBanwolSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicRedBanwolSkill, 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicRedBanwolSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicRedBanwolSkill.MagicInfo.wMagicID, m_MagicRedBanwolSkill.btLevel, m_MagicRedBanwolSkill.nTranPoint, "", 3000);
                        }
                    }
                    if ((wHitMode == 7) && (m_MagicFireSwordSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicFireSwordSkill.btLevel < 3) && (m_MagicFireSwordSkill.MagicInfo.TrainLevel[m_MagicFireSwordSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicFireSwordSkill, 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicFireSwordSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicFireSwordSkill.MagicInfo.wMagicID, m_MagicFireSwordSkill.btLevel, m_MagicFireSwordSkill.nTranPoint, "", 3000);
                        }
                    }
                    if ((wHitMode == 8) && (m_MagicCrsSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicCrsSkill.btLevel < 3) && (m_MagicCrsSkill.MagicInfo.TrainLevel[m_MagicCrsSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicCrsSkill, 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicCrsSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicCrsSkill.MagicInfo.wMagicID, m_MagicCrsSkill.btLevel, m_MagicCrsSkill.nTranPoint, "", 3000);
                        }
                    }
                    if ((wHitMode == 9) && (m_MagicTwnHitSkill != null) && (m_btRaceServer == Grobal2.RC_PLAYOBJECT) && (m_MagicTwnHitSkill.btLevel < 3) && (m_MagicTwnHitSkill.MagicInfo.TrainLevel[m_MagicTwnHitSkill.btLevel] <= m_Abil.Level))
                    {
                        (this as TPlayObject).TrainSkill(m_MagicTwnHitSkill, 1);
                        if (!(this as TPlayObject).CheckMagicLevelup(m_MagicTwnHitSkill))
                        {
                            SendDelayMsg(this, Grobal2.RM_MAGIC_LVEXP, 0, m_MagicTwnHitSkill.MagicInfo.wMagicID, m_MagicTwnHitSkill.btLevel, m_MagicTwnHitSkill.nTranPoint, "", 3000);
                        }
                    }
                    result = true;
                    if (M2Share.g_Config.boMonDelHptoExp)
                    {
                        if (m_btRaceServer == Grobal2.RC_PLAYOBJECT)
                        {
                            if (this.m_boAI)
                            {
                                if ((this as TAIPlayObject).m_WAbil.Level <= M2Share.g_Config.MonHptoExpLevel)
                                {
                                    if (!M2Share.GetNoHptoexpMonList(AttackTarget.m_sCharName))
                                    {
                                        (this as TAIPlayObject).GainExp(nPower * M2Share.g_Config.MonHptoExpmax);
                                    }
                                }
                            }
                            else
                            {
                                if ((this as TPlayObject).m_WAbil.Level <= M2Share.g_Config.MonHptoExpLevel)
                                {
                                    if (!M2Share.GetNoHptoexpMonList(AttackTarget.m_sCharName))
                                    {
                                        (this as TPlayObject).GainExp(nPower * M2Share.g_Config.MonHptoExpmax);
                                    }
                                }
                            }
                        }
                        if (m_btRaceServer == Grobal2.RC_PLAYCLONE)
                        {
                            if (m_Master != null)
                            {
                                if (m_Master.m_boAI)
                                {
                                    if ((m_Master as TAIPlayObject).m_WAbil.Level <= M2Share.g_Config.MonHptoExpLevel)
                                    {
                                        if (!M2Share.GetNoHptoexpMonList(AttackTarget.m_sCharName))
                                        {
                                            (m_Master as TAIPlayObject).GainExp(nPower * M2Share.g_Config.MonHptoExpmax);
                                        }
                                    }
                                }
                                else
                                {
                                    if ((m_Master as TPlayObject).m_WAbil.Level <= M2Share.g_Config.MonHptoExpLevel)
                                    {
                                        if (!M2Share.GetNoHptoexpMonList(AttackTarget.m_sCharName))
                                        {
                                            (m_Master as TPlayObject).GainExp(nPower * M2Share.g_Config.MonHptoExpmax);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if ((nWeaponDamage > 0) && (m_UseItems[Grobal2.U_WEAPON] != null) && (m_UseItems[Grobal2.U_WEAPON].wIndex > 0))
                {
                    DoDamageWeapon(nWeaponDamage);
                }
                if (AttackTarget.m_btRaceServer != Grobal2.RC_PLAYOBJECT)
                {
                    AttackTarget.SendMsg(AttackTarget, Grobal2.RM_STRUCK, (short)nPower, AttackTarget.m_WAbil.HP, AttackTarget.m_WAbil.MaxHP, ObjectId, "");
                }
            }
            catch (Exception e)
            {
                M2Share.ErrorMessage(e.Message);
            }
            return result;
        }
    }
}