﻿using GameSvr.Actor;
using GameSvr.Maps;
using GameSvr.Monster;
using GameSvr.Monster.Monsters;
using GameSvr.Npc;
using SystemModule;
using SystemModule.Data;
using SystemModule.Enums;
using SystemModule.Packets.ClientPackets;

namespace GameSvr.World
{
    public partial class WorldServer
    {
        /// <summary>
        /// 怪物刷新列表
        /// Key:线程ID
        /// Value:怪物列表
        /// </summary>
        public readonly Dictionary<int, IList<MonGenInfo>> MonGenInfoThreadMap;
        /// <summary>
        /// 怪物刷新列表
        /// </summary>
        public readonly IList<MonGenInfo> MonGenList;
        /// <summary>
        /// 怪物对应线程
        /// </summary>
        private readonly Dictionary<string, int> MonsterThreadMap;

        public MonsterThread[] MobThreads;
        private Thread[] MobThreading;
        private readonly object _locker = new object();

        public void InitializeMonster()
        {
            var monsterGenMap = new Dictionary<string, IList<MonGenInfo>>(StringComparer.OrdinalIgnoreCase); //临时存放怪物刷新映射,这样也能知道每一个怪要刷新几个和统计

            for (int i = 0; i < MonGenList.Count; i++)
            {
                var monName = MonGenList[i].MonName;
                if (monsterGenMap.ContainsKey(monName))
                {
                    monsterGenMap[monName].Add(MonGenList[i]);
                }
                else
                {
                    monsterGenMap.Add(monName, new List<MonGenInfo>() { MonGenList[i] });
                }
            }

            var monsterNames = monsterGenMap.Keys.ToList();
            for (int i = 0; i < monsterNames.Count; i++)
            {
                var threadId = M2Share.RandomNumber.Random(M2Share.Config.ProcessMonsterMultiThreadLimit);
                var monName = monsterNames[i];
                if (MonGenInfoThreadMap.ContainsKey(threadId))
                {
                    for (int j = 0; j < monsterGenMap[monName].Count; j++)
                    {
                        MonGenInfoThreadMap[threadId].Add(monsterGenMap[monName][j]);
                    }
                }
                else
                {
                    var monsterList = new List<MonGenInfo>();
                    for (int j = 0; j < monsterGenMap[monName].Count; j++)
                    {
                        monsterList.Add(monsterGenMap[monName][j]);
                    }
                    MonGenInfoThreadMap.Add(threadId, monsterList);
                }

                if (!MonsterThreadMap.ContainsKey(MonGenList[i].MonName))
                {
                    MonsterThreadMap.Add(MonGenList[i].MonName, threadId);
                }
            }

            var monsterName = MonsterList.Values.ToList();
            for (int i = 0; i < monsterName.Count; i++)
            {
                var threadId = M2Share.RandomNumber.Random(M2Share.Config.ProcessMonsterMultiThreadLimit);
                if (!MonsterThreadMap.ContainsKey(MonsterList[monsterName[i].Name].Name))
                {
                    MonsterThreadMap.Add(MonsterList[monsterName[i].Name].Name, threadId);
                }
            }

            for (var i = 0; i < MonGenInfoThreadMap.Count; i++)
            {
                for (var j = 0; j < MonGenInfoThreadMap[i].Count; j++)
                {
                    if (MonGenInfoThreadMap[i] != null)
                    {
                        if (string.IsNullOrEmpty(MonGenInfoThreadMap[i][j].MonName))
                        {
                            continue;
                        }
                        MonGenInfoThreadMap[i][j].Race = GetMonRace(MonGenInfoThreadMap[i][j].MonName);
                    }
                }
            }

            MonGenList.Clear();
        }

        public void Stop()
        {
            lock (_locker)
            {
                Monitor.PulseAll(_locker);
            }

            for (var i = 0; i < MobThreading.Length; i++)
            {
                if (MobThreads[i] != null)
                {
                    MobThreads[i].EndTime = HUtil32.GetTickCount() + 9999;
                }
                if (MobThreading[i] != null &&
                    MobThreading[i].ThreadState != ThreadState.Stopped && MobThreading[i].ThreadState != ThreadState.Unstarted)
                {
                    MobThreading[i].Interrupt();
                }
            }
        }

        /// <summary>
        /// 初始化怪物运行线程
        /// </summary>
        public void InitializationMonsterThread()
        {
            _logger.Info($"Monster Run threads:[{M2Share.Config.ProcessMonsterMultiThreadLimit}]");

            var monsterThreads = M2Share.Config.ProcessMonsterMultiThreadLimit; //处理线程+预留线程

            MobThreads = new MonsterThread[monsterThreads];
            MobThreading = new Thread[monsterThreads];

            for (var i = 0; i < M2Share.Config.ProcessMonsterMultiThreadLimit; i++)
            {
                MobThreads[i] = new MonsterThread();
                MobThreads[i].Id = i;
            }

            for (var i = 0; i < monsterThreads; i++)
            {
                var mobThread = MobThreads[i];
                if (mobThread == null)
                {
                    continue;
                }
                MobThreading[i] = new Thread(() => ProcessMonsters(mobThread)) { IsBackground = true };
                MobThreading[i].Start();
            }
        }

        /// <summary>
        /// 取怪物刷新时间
        /// </summary>
        /// <returns></returns>
        public int GetMonstersZenTime(int dwTime)
        {
            int result;
            if (dwTime < 30 * 60 * 1000)
            {
                var d10 = (PlayObjectCount - M2Share.Config.UserFull) / HUtil32._MAX(1, M2Share.Config.ZenFastStep);
                if (d10 > 0)
                {
                    if (d10 > 6)
                    {
                        d10 = 6;
                    }
                    //result = dwTime - dwTime / 10 * d10;
                    result = dwTime - HUtil32.Round((dwTime / 10) * d10);
                }
                else
                {
                    result = dwTime;
                }
            }
            else
            {
                result = dwTime;
            }
            return result;
        }

        /// <summary>
        /// 刷新怪物
        /// </summary>
        private void ProcessMonsters(MonsterThread monsterThread)
        {
            if (monsterThread == null)
            {
                return;
            }

            IList<MonGenInfo> mongenList;
            if (!MonGenInfoThreadMap.TryGetValue(monsterThread.Id, out mongenList))
            {
                return;
            }
            _logger.Info($"Monster Thread:{monsterThread.Id} Monsters:{mongenList.Count} starting work.");

            while (true)
            {
                MonGenInfo monGen = null;
                if ((HUtil32.GetTickCount() - monsterThread.RegenMonstersTick) > M2Share.Config.RegenMonstersTime)
                {
                    monsterThread.RegenMonstersTick = HUtil32.GetTickCount();
                    if (monsterThread.CurrMonGenIdx < mongenList.Count)
                    {
                        monGen = mongenList[monsterThread.CurrMonGenIdx];
                    }
                    else if (mongenList.Count > 0)
                    {
                        monGen = mongenList[0];
                    }
                    if (monsterThread.CurrMonGenIdx < mongenList.Count - 1)
                    {
                        monsterThread.CurrMonGenIdx++;
                    }
                    else
                    {
                        monsterThread.CurrMonGenIdx = 0;
                    }
                    if (monGen != null && !string.IsNullOrEmpty(monGen.MonName) && !M2Share.Config.VentureServer)
                    {
                        if (monGen.StartTick == 0 || ((HUtil32.GetTickCount() - monGen.StartTick) > GetMonstersZenTime(monGen.ZenTime)))
                        {
                            var nGenCount = monGen.ActiveCount; //取已刷出来的怪数量
                            var boRegened = true;
                            var genModCount = HUtil32._MAX(1, HUtil32.Round(HUtil32._MAX(1, monGen.Count) / (M2Share.Config.MonGenRate / 10)));//所需刷的怪总数
                            var map = M2Share.MapMgr.FindMap(monGen.MapName);
                            bool canCreate;
                            if (map == null || map.Flag.boNOHUMNOMON && map.HumCount <= 0)
                                canCreate = false;
                            else
                                canCreate = true;
                            if (genModCount > nGenCount && canCreate)// 增加 控制刷怪数量比例
                            {
                                boRegened = RegenMonsters(monGen, genModCount - nGenCount);
                            }
                            if (boRegened)
                            {
                                monGen.StartTick = HUtil32.GetTickCount();
                            }
                        }
                    }
                }

                var dwRunTick = HUtil32.GetTickCount();
                try
                {
                    var boProcessLimit = false;
                    var dwCurrentTick = HUtil32.GetTickCount();
                    var dwMonProcTick = HUtil32.GetTickCount();
                    monsterThread.MonsterProcessCount = 0;
                    var i = 0;
                    for (i = monsterThread.MonGenListPosition; i < mongenList.Count; i++)
                    {
                        monGen = mongenList[i];
                        var processPosition = monsterThread.MonGenCertListPosition < monGen.CertList.Count ? monsterThread.MonGenCertListPosition : 0;
                        monsterThread.MonGenCertListPosition = 0;
                        while (true)
                        {
                            if (processPosition >= monGen.CertList.Count)
                            {
                                break;
                            }
                            var monster = (AnimalObject)monGen.CertList[processPosition];
                            if (monster != null)
                            {
                                if (!monster.Ghost)
                                {
                                    if ((dwCurrentTick - monster.RunTick) > monster.RunTime)
                                    {
                                        monster.RunTick = dwRunTick;
                                        if (monster.Death && monster.CanReAlive && monster.Invisible && (monster.MonGen != null))
                                        {
                                            if ((HUtil32.GetTickCount() - monster.ReAliveTick) > GetMonstersZenTime(monster.MonGen.ZenTime))
                                            {
                                                if (monster.ReAliveEx(monster.MonGen))
                                                {
                                                    monster.ReAliveTick = HUtil32.GetTickCount();
                                                }
                                            }
                                        }
                                        if (!monster.IsVisibleActive && (monster.ProcessRunCount < M2Share.Config.ProcessMonsterInterval))
                                        {
                                            monster.ProcessRunCount++;
                                        }
                                        else
                                        {
                                            if ((dwCurrentTick - monster.SearchTick) > monster.SearchTime)
                                            {
                                                monster.SearchTick = HUtil32.GetTickCount();
                                                if (!monster.Death)
                                                {
                                                    //怪物主动搜索视觉范围，修改为被动搜索，能够降低CPU和内存使用率，从而提升效率
                                                    //要区分哪些怪物是主动攻击，哪些怪物是被动攻击
                                                    //被动攻击怪物主要代表为 鹿 鸡 祖玛雕像（石化状态）
                                                    //其余怪物均为主动攻击
                                                    //修改为被动攻击后，由玩家或者下属才执行SearchViewRange方法,找到怪物之后加入到怪物视野范围
                                                    //由玩家找出附近的怪物，然后添加到怪物列表
                                                    //monster.SearchViewRange();

                                                    if (monster.IsSlave) //如果是作为玩家的下属，也有主动搜索附近的精灵
                                                    {
                                                        if (monster.VisibleActors.Count > 0)//优先使用上一次的视觉范围,下属需要注意如果离角色远然后飞到角色身边的时候可能会使用上一次的视觉范围
                                                        {
                                                            continue;
                                                        }
                                                        monster.SearchViewRange();
                                                        continue;
                                                    }
                                                    if (monster.Race is ActorRace.Guard or ActorRace.ArcherGuard or ActorRace.SlaveMonster) //守卫才主动搜索附近的精灵
                                                    {
                                                        if (monster.VisibleActors.Count > 0)//优先使用上一次的视觉范围
                                                        {
                                                            continue;
                                                        }
                                                        monster.SearchViewRange();
                                                    }
                                                    continue;
                                                }
                                                monster.SearchViewRangeDeath();
                                            }
                                            monster.ProcessRunCount = 0;
                                            monster.Run();
                                        }
                                    }
                                    monsterThread.MonsterProcessPostion++;
                                }
                                else
                                {
                                    if ((HUtil32.GetTickCount() - monster.GhostTick) > 5 * 60 * 1000)
                                    {
                                        monGen.CertList.RemoveAt(processPosition);
                                        monGen.CertCount--;
                                        monster = null;
                                        continue;
                                    }
                                }
                            }
                            processPosition++;
                            if ((HUtil32.GetTickCount() - dwMonProcTick) > M2Share.MonLimit)
                            {
                                boProcessLimit = true;
                                monsterThread.MonGenCertListPosition = processPosition;
                                break;
                            }
                        }
                        if (boProcessLimit) break;
                    }
                    if (MonGenInfoThreadMap.Count <= i)
                    {
                        monsterThread.MonGenListPosition = 0;
                        monsterThread.MonsterCount = monsterThread.MonsterProcessPostion;
                        monsterThread.MonsterProcessPostion = 0;
                    }
                    monsterThread.MonGenListPosition = !boProcessLimit ? 0 : i;
                }
                catch (Exception e)
                {
                    _logger.Error(e.StackTrace);
                }
                finally
                {
                    Thread.Sleep(20);
                }
            }
        }

        /// <summary>
        /// 获取刷怪数量
        /// </summary>
        /// <param name="monGen"></param>
        /// <returns></returns>
        private int GetGenMonCount(MonGenInfo monGen)
        {
            var nCount = 0;
            for (var i = 0; i < monGen.CertList.Count; i++)
            {
                BaseObject baseObject = monGen.CertList[i];
                if (!baseObject.Death && !baseObject.Ghost)
                {
                    nCount++;
                }
            }
            return nCount;
        }

        public BaseObject RegenMonsterByName(string sMap, short nX, short nY, string sMonName)
        {
            var nRace = GetMonRace(sMonName);
            var baseObject = CreateMonster(sMap, nX, nY, nRace, sMonName);
            if (baseObject != null)
            {
                var threadId = GetMonsterThreadId(sMonName);
                if (threadId >= 0)
                {
                    MonGenInfo MonGenInfo = new MonGenInfo();
                    MonGenInfo.MapName = sMap;
                    MonGenInfo.X = nX;
                    MonGenInfo.Y = nY;
                    MonGenInfo.MonName = sMonName;
                    MonGenInfo.Range = 0;
                    MonGenInfo.Count = 1;
                    MonGenInfo.ZenTime = 0;
                    MonGenInfo.MissionGenRate = 0;// 集中座标刷新机率 1 -100
                    MonGenInfo.CertList = new List<BaseObject>();
                    MonGenInfo.Envir = M2Share.MapMgr.FindMap(MonGenInfo.MapName);
                    if (MonGenInfo.TryAdd(baseObject))
                    {
                        MonGenInfo.CertCount++;
                    }
                    MonGenInfoThreadMap[threadId].Add(MonGenInfo);

                    //var n18 = MonGenInfoThreadMap[threadId].Count - 1;
                    //if (n18 < 0) n18 = 0;
                    //if (MonGenInfoThreadMap[threadId].Count > n18)
                    //{
                    //    var monGen = MonGenInfoThreadMap[threadId][n18];
                    //    if (monGen.TryAdd(baseObject))
                    //    {
                    //        monGen.CertCount++;
                    //    }
                    //    else
                    //    {
                    //        threadId = M2Share.Config.ProcessMonsterMultiThreadLimit + 1;
                    //        MonGenInfoThreadMap.Add(threadId, new List<MonGenInfo> { monGen.Clone() });//todo 启动线程
                    //    }
                    //}
                    baseObject.Envir.AddObject(baseObject);
                    baseObject.AddToMaped = true;
                }
                else
                {
                    return null;
                }
            }
            return baseObject;
        }

        /// <summary>
        /// 计算怪物掉落物品
        /// 即创建怪物对象的时候已经算好要掉落的物品和属性
        /// </summary>
        /// <returns></returns>
        private void MonGetRandomItems(BaseObject mon)
        {
            IList<TMonItem> itemList = null;
            var itemName = string.Empty;
            if (MonsterList.TryGetValue(mon.ChrName, out var monster))
            {
                itemList = monster.ItemList;
            }
            if (itemList != null)
            {
                for (var i = 0; i < itemList.Count; i++)
                {
                    var monItem = itemList[i];
                    if (M2Share.RandomNumber.Random(monItem.MaxPoint) <= monItem.SelPoint)
                    {
                        if (string.Compare(monItem.ItemName, Grobal2.sSTRING_GOLDNAME, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            mon.Gold = mon.Gold + monItem.Count / 2 + M2Share.RandomNumber.Random(monItem.Count);
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(itemName)) itemName = monItem.ItemName;
                            UserItem userItem = null;
                            if (CopyToUserItemFromName(itemName, ref userItem))
                            {
                                userItem.Dura = (ushort)HUtil32.Round(userItem.DuraMax / 100 * (20 + M2Share.RandomNumber.Random(80)));
                                var stdItem = GetStdItem(userItem.Index);
                                if (stdItem == null) continue;
                                if (M2Share.RandomNumber.Random(M2Share.Config.MonRandomAddValue) == 0) //极品掉落几率
                                {
                                    stdItem.RandomUpgradeItem(userItem);
                                }
                                if (M2Share.StdModeMap.Contains(stdItem.StdMode))
                                {
                                    if (stdItem.Shape == 130 || stdItem.Shape == 131 || stdItem.Shape == 132)
                                    {
                                        stdItem.RandomSetUnknownItem(userItem);
                                    }
                                }
                                mon.ItemList.Add(userItem);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 创建对象
        /// </summary>
        /// <returns></returns>
        private BaseObject CreateMonster(string sMapName, short nX, short nY, int nMonRace, string sMonName)
        {
            int n1C;
            short n20;
            short n24;
            BaseObject outofrange;
            BaseObject cert = null;
            var map = M2Share.MapMgr.FindMap(sMapName);
            if (map == null) return null;
            switch (nMonRace)
            {
                case ActorRace.Supreguard:
                    cert = new SuperGuard();
                    break;
                case ActorRace.Petsupreguard:
                    cert = new PetSuperGuard();
                    break;
                case ActorRace.ArcherPolice:
                    cert = new ArcherPolice();
                    break;
                case ActorRace.AnimalChicken:
                    cert = new MonsterObject
                    {
                        Animal = true,
                        MeatQuality = (ushort)(M2Share.RandomNumber.Random(3500) + 3000),
                        BodyLeathery = 50
                    };
                    break;
                case ActorRace.AnimalDeer:
                    if (M2Share.RandomNumber.Random(30) == 0)
                    {
                        cert = new ChickenDeer
                        {
                            Animal = true,
                            MeatQuality = (ushort)(M2Share.RandomNumber.Random(20000) + 10000),
                            BodyLeathery = 150
                        };
                    }
                    else
                    {
                        cert = new MonsterObject()
                        {
                            Animal = true,
                            MeatQuality = (ushort)(M2Share.RandomNumber.Random(8000) + 8000),
                            BodyLeathery = 150
                        };
                    }
                    break;
                case ActorRace.AnimalWolf:
                    cert = new AtMonster
                    {
                        Animal = true,
                        MeatQuality = (ushort)(M2Share.RandomNumber.Random(8000) + 8000),
                        BodyLeathery = 150
                    };
                    break;
                case ActorRace.Trainer:
                    cert = new Trainer();
                    break;
                case ActorRace.MonsterOma:
                    cert = new MonsterObject();
                    break;
                case ActorRace.MonsterOmaknight:
                    cert = new AtMonster();
                    break;
                case ActorRace.MonsterSpitspider:
                    cert = new SpitSpider();
                    break;
                case 83:
                    cert = new SlowAtMonster();
                    break;
                case 84:
                    cert = new Scorpion();
                    break;
                case ActorRace.MonsterStick:
                    cert = new StickMonster();
                    break;
                case 86:
                    cert = new AtMonster();
                    break;
                case ActorRace.MonsterDualaxe:
                    cert = new DualAxeMonster();
                    break;
                case 88:
                    cert = new AtMonster();
                    break;
                case 89:
                    cert = new AtMonster();
                    break;
                case 90:
                    cert = new GasAttackMonster();
                    break;
                case 91:
                    cert = new MagCowMonster();
                    break;
                case 92:
                    cert = new CowKingMonster();
                    break;
                case ActorRace.MonsterThonedark:
                    cert = new ThornDarkMonster();
                    break;
                case ActorRace.MonsterLightzombi:
                    cert = new LightingZombi();
                    break;
                case ActorRace.MonsterDigoutzombi:
                    cert = new DigOutZombi();
                    if (M2Share.RandomNumber.Random(2) == 0) cert.BoFearFire = true;
                    break;
                case ActorRace.MonsterZilkinzombi:
                    cert = new ZilKinZombi();
                    if (M2Share.RandomNumber.Random(4) == 0) cert.BoFearFire = true;
                    break;
                case 97:
                    cert = new CowMonster();
                    if (M2Share.RandomNumber.Random(2) == 0) cert.BoFearFire = true;
                    break;
                case ActorRace.MonsterWhiteskeleton:
                    cert = new WhiteSkeleton();
                    break;
                case ActorRace.MonsterSculture:
                    cert = new ScultureMonster
                    {
                        BoFearFire = true
                    };
                    break;
                case ActorRace.MonsterScultureking:
                    cert = new ScultureKingMonster();
                    break;
                case ActorRace.MonsterBeequeen:
                    cert = new BeeQueen();
                    break;
                case 104:
                    cert = new ArcherMonster();
                    break;
                case 105:
                    cert = new GasMothMonster();
                    break;
                case 106: // 楔蛾
                    cert = new GasDungMonster();
                    break;
                case 107:
                    cert = new CentipedeKingMonster();
                    break;
                case ActorRace.SabukDoor:
                    cert = new CastleDoor();
                    break;
                case ActorRace.SabukWall:
                    cert = new WallStructure();
                    break;
                case ActorRace.MonsterArcherguard:
                    cert = new ArcherGuard();
                    break;
                case ActorRace.MonsterElfmonster:
                    cert = new ElfMonster();
                    break;
                case ActorRace.MonsterElfwarrior:
                    cert = new ElfWarriorMonster();
                    break;
                case 115:
                    cert = new BigHeartMonster();
                    break;
                case 116:
                    cert = new SpiderHouseMonster();
                    break;
                case 117:
                    cert = new ExplosionSpider();
                    break;
                case 118:
                    cert = new HighRiskSpider();
                    break;
                case 119:
                    cert = new BigPoisionSpider();
                    break;
                case 120:
                    cert = new SoccerBall();
                    break;
                case 130:
                    cert = new DoubleCriticalMonster();
                    break;
                case 131:
                    cert = new RonObject();
                    break;
                case 132:
                    cert = new SandMobObject();
                    break;
                case 133:
                    cert = new MagicMonObject();
                    break;
                case 134:
                    cert = new BoneKingMonster();
                    break;
                case 200:
                    cert = new ElectronicScolpionMon();
                    break;
                case 201:
                    cert = new CloneMonster();
                    break;
                case 203:
                    cert = new TeleMonster();
                    break;
                case 206:
                    cert = new Khazard();
                    break;
                case 208:
                    cert = new GreenMonster();
                    break;
                case 209:
                    cert = new RedMonster();
                    break;
                case 210:
                    cert = new FrostTiger();
                    break;
                case 214:
                    cert = new FireMonster();
                    break;
                case 215:
                    cert = new FireballMonster();
                    break;
            }

            if (cert != null)
            {
                ApplyMonsterAbility(cert, sMonName);
                cert.Envir = map;
                cert.MapName = sMapName;
                cert.CurrX = nX;
                cert.CurrY = nY;
                cert.Direction = M2Share.RandomNumber.RandomByte(8);
                cert.ChrName = sMonName;
                cert.WAbil = cert.Abil;
                cert.OnEnvirnomentChanged();
                if (M2Share.RandomNumber.Random(100) < cert.CoolEyeCode) cert.CoolEye = true;
                MonGetRandomItems(cert);
                cert.Initialize();
                if (cert.AddtoMapSuccess)
                {
                    outofrange = null;
                    n20 = cert.Envir.Width < 50 ? (short)2 : (short)3;
                    if (cert.Envir.Height < 250)
                    {
                        n24 = cert.Envir.Height < 30 ? (short)2 : (short)20;
                    }
                    else
                    {
                        n24 = 50;
                    }
                    n1C = 0;
                    while (true)
                    {
                        if (!cert.Envir.CanWalk(cert.CurrX, cert.CurrY, false))
                        {
                            if ((cert.Envir.Width - n24 - 1) > cert.CurrX)
                            {
                                cert.CurrX += n20;
                            }
                            else
                            {
                                cert.CurrX = (short)(M2Share.RandomNumber.Random(cert.Envir.Width / 2) + n24);
                                if (cert.Envir.Height - n24 - 1 > cert.CurrY)
                                    cert.CurrY += n20;
                                else
                                    cert.CurrY = (short)(M2Share.RandomNumber.Random(cert.Envir.Height / 2) + n24);
                            }
                        }
                        else
                        {
                            outofrange = (BaseObject)cert.Envir.AddToMap(cert.CurrX, cert.CurrY, CellType.Monster, cert);
                            break;
                        }
                        n1C++;
                        if (n1C >= 31) break;
                    }
                    if (outofrange == null)
                    {
                        //_logger.Error($"创建怪物失败 名称:{sMonName} 地图:[{sMapName}] X:{nX} Y:{nY} ");
                        return null;
                    }
                }
            }
            return cert;
        }

        /// <summary>
        /// 创建怪物对象
        /// 在指定时间内创建完对象，则返加TRUE，如果超过指定时间则返回FALSE
        /// </summary>
        /// <returns></returns>
        private bool RegenMonsters(MonGenInfo monGen, int nCount)
        {
            BaseObject cert;
            const string sExceptionMsg = "[Exception] TUserEngine::RegenMonsters";
            var result = true;
            var dwStartTick = HUtil32.GetTickCount();
            try
            {
                if (monGen.Race > 0)
                {
                    short nX;
                    short nY;
                    if (monGen.MissionGenRate > 0 && M2Share.RandomNumber.Random(100) < monGen.MissionGenRate)
                    {
                        nX = (short)(monGen.X - monGen.Range + M2Share.RandomNumber.Random(monGen.Range * 2 + 1));
                        nY = (short)(monGen.Y - monGen.Range + M2Share.RandomNumber.Random(monGen.Range * 2 + 1));
                        for (var i = 0; i < nCount; i++)
                        {
                            cert = CreateMonster(monGen.MapName, (short)(nX - 10 + M2Share.RandomNumber.Random(20)), (short)(nY - 10 + M2Share.RandomNumber.Random(20)), monGen.Race, monGen.MonName);
                            if (cert != null)
                            {
                                cert.CanReAlive = true;
                                cert.ReAliveTick = HUtil32.GetTickCount();
                                cert.MonGen = monGen;
                                monGen.ActiveCount++;
                                monGen.TryAdd(cert);
                            }
                            if ((HUtil32.GetTickCount() - dwStartTick) > M2Share.ZenLimit)
                            {
                                result = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (var i = 0; i < nCount; i++)
                        {
                            nX = (short)((monGen.X - monGen.Range) + M2Share.RandomNumber.Random(monGen.Range * 2 + 1));
                            nY = (short)((monGen.Y - monGen.Range) + M2Share.RandomNumber.Random(monGen.Range * 2 + 1));
                            cert = CreateMonster(monGen.MapName, nX, nY, monGen.Race, monGen.MonName);
                            if (cert != null)
                            {
                                cert.CanReAlive = true;
                                cert.ReAliveTick = HUtil32.GetTickCount();
                                cert.MonGen = monGen;
                                monGen.ActiveCount++;
                                monGen.TryAdd(cert);
                            }
                            else
                            {
                                return false;
                            }
                            if (HUtil32.GetTickCount() - dwStartTick > M2Share.ZenLimit)
                            {
                                result = false;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                _logger.Error(sExceptionMsg);
            }
            return result;
        }

        private void ApplyMonsterAbility(BaseObject baseObject, string sMonName)
        {
            if (MonsterList.TryGetValue(sMonName, out var monster))
            {
                baseObject.Race = monster.Race;
                baseObject.RaceImg = monster.RaceImg;
                baseObject.Appr = monster.Appr;
                baseObject.Abil.Level = monster.Level;
                baseObject.LifeAttrib = monster.btLifeAttrib;
                baseObject.CoolEyeCode = monster.CoolEye;
                baseObject.FightExp = monster.Exp;
                baseObject.Abil.HP = monster.HP;
                baseObject.Abil.MaxHP = monster.HP;
                baseObject.MonsterWeapon = HUtil32.LoByte(monster.MP);
                baseObject.Abil.MP = 0;
                baseObject.Abil.MaxMP = monster.MP;
                baseObject.Abil.AC = HUtil32.MakeWord(monster.AC, monster.AC);
                baseObject.Abil.MAC = HUtil32.MakeWord(monster.MAC, monster.MAC);
                baseObject.Abil.DC = HUtil32.MakeWord(monster.DC, monster.MaxDC);
                baseObject.Abil.MC = HUtil32.MakeWord(monster.MC, monster.MC);
                baseObject.Abil.SC = HUtil32.MakeWord(monster.SC, monster.SC);
                baseObject.SpeedPoint = monster.Speed;
                baseObject.HitPoint = monster.HitPoint;

                baseObject.WalkSpeed = monster.WalkSpeed;
                baseObject.WalkStep = monster.WalkStep;
                baseObject.WalkWait = monster.WalkWait;
                baseObject.NextHitTime = monster.AttackSpeed;

                baseObject.NastyMode = monster.boAggro;
                baseObject.NoTame = monster.boTame;
            }
        }
    }
}