using System;
using SystemModule;
using SystemModule.Consts;

namespace BotSvr.Objects
{
    public class HeroActor
    {
        private const int Overdisc = 22;
        private readonly RobotClient _robotClient;
        private long _gHinttick1;
        private long _gHinttick2;

        public HeroActor(RobotClient robotClient)
        {
            this._robotClient = robotClient;
        }

        private bool CanNextSpell()
        {
            var result = false;
            if (MShare.g_boSpeedRate)
            {
                if (MShare.GetTickCount() - MShare.g_dwLatestSpellTick > MShare.g_dwSpellTime + MShare.g_dwMagicDelayTime - MShare.g_MagSpeedRate * 20)
                {
                    result = true;
                }
            }
            else
            {
                if (MShare.GetTickCount() - MShare.g_dwLatestSpellTick > MShare.g_dwSpellTime + MShare.g_dwMagicDelayTime)
                {
                    result = true;
                }
            }
            return result;
        }

        private void Enterqueue(MapTree node, int f)
        {
            var p = MShare.g_APQueue;
            var father = p;
            while (f > p.F)
            {
                father = p;
                p = p.Next;
                if (p == null) break;
            }
            var q = new MapLink();
            q.F = f;
            q.Node = node;
            q.Next = p;
            father.Next = q;
        }

        /// <summary>
        /// 将离目的地估计最近的方案出队列
        /// </summary>
        /// <returns></returns>
        private MapTree Dequeue()
        {
            var bestchoice = MShare.g_APQueue.Next.Node;
            MShare.g_APQueue.Next = MShare.g_APQueue.Next.Next;
            Dispose(MShare.g_APQueue.Next);
            return bestchoice;
        }

        /// <summary>
        /// 释放申请过的所有节点
        /// </summary>
        private void FreeTree()
        {
            while (MShare.g_APQueue != null)
            {
                var p = MShare.g_APQueue;
                if (p.Node != null) Dispose(p.Node);
                p.Node = null;
                MShare.g_APQueue = MShare.g_APQueue.Next;
                Dispose(p);
            }
        }

        private void Dispose(object obj)
        {
            obj = null;
        }

        // 估价函数,估价 x,y 到目的地的距离,估计值必须保证比实际值小
        private int Judge(int x, int y, int endX, int endY)
        {
            return Math.Abs(endX - x) + Math.Abs(endY - y);
        }

        private bool TryTileHas(int x, int y, int h)
        {
            var cx = x - _robotClient.Map.m_nBlockLeft;
            var cy = y - _robotClient.Map.m_nBlockTop;
            if (cx > MShare.MAXX * 3 || cy > MShare.MAXY * 3) return true;
            if (cx < 0 || cy < 0) return true;
            if (h < MShare.g_APPass[cx, cy])
            {
                return false;
            }
            return true;
        }

        private void Trytile(int x, int y, int endX, int endY, MapTree father, byte dir)
        {
            if (!_robotClient.Map.CanMove(x, y))
            {
                return;
            }
            MapTree p = father;
            while (p != null)
            {
                if (x == p.X && y == p.Y)
                {
                    return; // 如果 (x,y) 曾经经过,失败
                }
                p = p.Father;
            }
            var h = (ushort)(father.H + 1);
            if (TryTileHas(x, y, h))// 如果曾经有更好的方案移动到 (x,y) 失败
            {
                return;
            }
            MShare.g_APPass[x - _robotClient.Map.m_nBlockLeft, y - _robotClient.Map.m_nBlockTop] = h;// 记录这次到 (x,y) 的距离为历史最佳距离
            p = new MapTree();
            p.Father = father;
            p.H = father.H + 1;
            p.X = x;
            p.Y = y;
            p.Dir = dir;
            Enterqueue(p, p.H + Judge(x, y, endX, endY));
        }

        /// <summary>
        /// 路径寻找
        /// </summary>
        public void AutoFindPath(int startx, int starty, int endX, int endY)
        {
            if (!_robotClient.Map.CanMove(endX, endY))
            {
                return;
            }
            MShare.g_APPass = (ushort[,])MShare.g_APPassEmpty.Clone();
            Init_Queue();
            MapTree root = new MapTree();
            root.X = startx;
            root.Y = starty;
            root.H = 0;
            root.Father = null;
            Enterqueue(root, Judge(startx, starty, endX, endY));
            var tryCount = 0;
            while (true)
            {
                root = Dequeue();
                if (root == null) break;
                var x = root.X;
                var y = root.Y;
                if (x == endX && y == endY)
                {
                    break;
                }
                Trytile(x, y - 1, endX, endY, root, 0); // 尝试向上移动
                Trytile(x + 1, y - 1, endX, endY, root, 1); // 尝试向右上移动
                Trytile(x + 1, y, endX, endY, root, 2); // 尝试向右移动
                Trytile(x + 1, y + 1, endX, endY, root, 3); // 尝试向右下移动
                Trytile(x, y + 1, endX, endY, root, 4); // 尝试向下移动
                Trytile(x - 1, y + 1, endX, endY, root, 5); // 尝试向左下移动
                Trytile(x - 1, y, endX, endY, root, 6); // 尝试向左移动
                Trytile(x - 1, y - 1, endX, endY, root, 7); // 尝试向左上移动
                tryCount++;
                if (tryCount > 100)
                {
                    Console.WriteLine("自动寻路算法出错,停止移动。");
                    break;
                }
            }
            for (var i = MShare.g_APPathList.Count - 1; i >= 0; i--)
            {
                Dispose(MShare.g_APPathList[i]);
            }
            MShare.g_APPathList.Clear();
            if (root == null)
            {
                FreeTree();
                return;
            }
            var temp = new FindMapNode();
            temp.X = root.X;
            temp.Y = root.Y;
            MShare.g_APPathList.Add(temp);
            int dir = root.Dir;
            var p = root;
            root = root.Father;
            while (root != null)
            {
                if (dir != root.Dir)
                {
                    temp = new FindMapNode();
                    temp.X = p.X;
                    temp.Y = p.Y;
                    MShare.g_APPathList.Insert(0, temp);
                    dir = root.Dir;
                }
                p = root;
                root = root.Father;
            }
            FreeTree();
        }

        public int RandomRange(int aFrom, int aTo)
        {
            int result;
            if (aFrom > aTo)
                result = new Random(aFrom - aTo).Next() + aTo;
            else
                result = new Random(aTo - aFrom).Next() + aFrom;
            return result;
        }

        private void Init_Queue()
        {
            FreeTree();
            if (MShare.g_APQueue != null)
            {
                if (MShare.g_APQueue.Next != null)
                {
                    Dispose(MShare.g_APQueue.Next);
                }
                MShare.g_APQueue.Next = null;
                if (MShare.g_APQueue.Node != null)
                {
                    Dispose(MShare.g_APQueue.Node);
                }
                MShare.g_APQueue.Node = null;
                Dispose(MShare.g_APQueue);
                MShare.g_APQueue = null;
            }
            MShare.g_APQueue = new MapLink();
            MShare.g_APQueue.Node = null;
            MShare.g_APQueue.F = -1;
            MShare.g_APQueue.Next = new MapLink();
            MShare.g_APQueue.Next.F = 0xFFFFFFF;
            MShare.g_APQueue.Next.Node = null;
            MShare.g_APQueue.Next.Next = null;
            for (var i = MShare.g_APPathList.Count - 1; i >= 0; i--)
            {
                Dispose(MShare.g_APPathList[i]);
            }
            MShare.g_APPathList.Clear();
        }

        public void Init_Queue2()
        {
            FreeTree();
            if (MShare.g_APQueue != null)
            {
                if (MShare.g_APQueue.Next != null)
                    Dispose(MShare.g_APQueue.Next);
                MShare.g_APQueue.Next = null;
                if (MShare.g_APQueue.Node != null)
                    Dispose(MShare.g_APQueue.Node);
                MShare.g_APQueue.Node = null;
                Dispose(MShare.g_APQueue);
                MShare.g_APQueue = null;
            }
            for (var i = MShare.g_APPathList.Count - 1; i >= 0; i--)
            {
                Dispose(MShare.g_APPathList[i]);
            }
            MShare.g_APPathList.Clear();
        }

        private bool IsBackToSafeZone(ref int ret)
        {
            bool has;
            bool result = false;
            ret = 0;
            if (MShare.g_gcAss[1])
            {
                // 红没有回城
                has = false;
                for (var i = 0; i < MShare.MAXBAGITEMCL; i++)
                {
                    if (MShare.g_ItemArr[i].Item.Name != "" && MShare.g_ItemArr[i].Item.AC > 0 && MShare.g_ItemArr[i].Item.StdMode == 0)
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    ret = 1;
                    result = true;
                    return result;
                }
            }

            if (MShare.g_gcAss[2])
            {
                // 蓝没有回城
                has = false;
                for (var i = 0; i < MShare.MAXBAGITEMCL; i++)
                {
                    if (MShare.g_ItemArr[i].Item.Name != "" && MShare.g_ItemArr[i].Item.MAC > 0 &&
                        MShare.g_ItemArr[i].Item.StdMode == 0)
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    ret = 2;
                    result = true;
                    return result;
                }
            }

            // 包裹满没有回城
            if (MShare.g_gcAss[4])
            {
                has = false;
                for (var i = 0; i < 45; i++)
                {
                    if (MShare.g_ItemArr[i].Item.Name == "")
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    ret = 3;
                    result = true;
                    return result;
                }
            }

            // 符没有回城
            if (MShare.g_gcAss[3])
            {
                has = false;
                for (var i = 0; i < MShare.MAXBAGITEMCL; i++)
                {
                    if (MShare.g_ItemArr[i].Item.StdMode == 25 && MShare.g_ItemArr[i].Item.Name != "" && MShare.g_ItemArr[i].Item.Name.IndexOf("符", StringComparison.Ordinal) > 0)
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    ret = 4;
                    result = true;
                    return result;
                }
                has = false;
                for (var i = 0; i < MShare.MAXBAGITEMCL; i++)
                {
                    if (MShare.g_ItemArr[i].Item.StdMode == 25 && MShare.g_ItemArr[i].Item.Name != "" && MShare.g_ItemArr[i].Item.Name.IndexOf("药", StringComparison.Ordinal) > 0)
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    ret = 5;
                    result = true;
                    return result;
                }
            }
            return result;
        }

        private int GetDis(int x1, int y1, int x2, int y2)
        {
            return (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
        }

        public bool IsProperTarget(TActor actor)
        {
            return actor != null && actor.m_btRace != 0 && !string.IsNullOrEmpty(actor.m_sUserName) &&
                   (actor.m_btRace != 12 || actor.m_btRace != 50) && !actor.m_boDeath && actor.m_btRace != 12 &&
                   (actor.m_nState & PoisonState.STONEMODE) == 0 && actor.m_sUserName.IndexOf("(", StringComparison.OrdinalIgnoreCase) == -1 &&
                   actor.m_boVisible && !actor.m_boDelActor && !actor.m_btAFilter && MShare.g_gcAss[6] &&
                   !MShare.g_APMobList.ContainsKey(actor.m_sUserName);
        }

        /// <summary>
        /// 搜索附近的对象
        /// </summary>
        /// <returns></returns>
        private TActor SearchTarget()
        {
            TActor result = null;
            var distance = 10000;
            if (MShare.g_APTagget != null)
            {
                if (!MShare.g_APTagget.m_boDeath && MShare.g_APTagget.m_nHiterCode == MShare.g_MySelf.m_nRecogId && MShare.g_APTagget.m_boVisible && !MShare.g_APTagget.m_boDelActor)
                {
                    distance = GetDis(MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY, MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY);
                    result = MShare.g_APTagget;
                }
            }
            for (var i = 0; i < _robotClient.g_PlayScene.m_ActorList.Count; i++)
            {
                var actor = _robotClient.g_PlayScene.m_ActorList[i];
                if (IsProperTarget(actor))
                {
                    var dx = GetDis(actor.m_nCurrX, actor.m_nCurrY, MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY);
                    if (dx < distance)
                    {
                        distance = dx;
                        result = actor;
                    }
                }
            }
            return result;
        }

        private int GetDropItemsDis()
        {
            int result = 100000;
            for (var i = 0; i < MShare.g_DropedItemList.Count; i++)
            {
                TDropItem d = MShare.g_DropedItemList[i];
                if (MShare.g_boPickUpAll || d.boPickUp)// 如果拾取过滤，则判断是否过滤
                {
                    var dx = GetDis(d.X, d.Y, MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY);
                    if (dx < result && dx != 0) // 获取距离，选择最近的
                    {
                        MShare.g_AutoPicupItem = d;
                        result = dx;
                    }
                }
            }
            return result;
        }

        public int GetAutoPalyStation()
        {
            int result;
            var mobdistance = 0;
            bool bPcaketfull = false;
            if (IsBackToSafeZone(ref mobdistance))
            {
                result = 0;
                return result;
            }
            bool has = false;
            for (var i = 0; i < 45; i++)
            {
                if (MShare.g_ItemArr[i] == null || MShare.g_ItemArr[i].Item.Name == "")
                {
                    has = true;
                    break;
                }
            }
            if (!has) // 包满
            {
                bPcaketfull = true;
            }
            if (MShare.g_nOverAPZone > 0)
            {
                result = 4;
                return result;
            }
            if (MShare.g_APMapPath != null && MShare.g_APStep >= 0 && MShare.g_APStep < MShare.g_APMapPath.Length) // 正在循路，超出范围。。。
            {
                if (MShare.g_APLastPoint.X >= 0)
                {
                    if ((Math.Abs(MShare.g_APLastPoint.X - MShare.g_MySelf.m_nCurrX) >= Overdisc ||
                         Math.Abs(MShare.g_APLastPoint.X - MShare.g_MySelf.m_nCurrY) >= Overdisc) &&
                        (Math.Abs(MShare.g_APMapPath[MShare.g_APStep].X - MShare.g_MySelf.m_nCurrX) >= Overdisc ||
                         Math.Abs(MShare.g_APMapPath[MShare.g_APStep].X - MShare.g_MySelf.m_nCurrY) >= Overdisc))
                    {
                        MShare.g_nOverAPZone = 14;
                        result = 4;
                        return result;
                    }
                }
                else
                {
                    if (Math.Abs(MShare.g_APMapPath[MShare.g_APStep].X - MShare.g_MySelf.m_nCurrX) >= Overdisc || Math.Abs(MShare.g_APMapPath[MShare.g_APStep].X - MShare.g_MySelf.m_nCurrY) >= Overdisc)
                    {
                        MShare.g_nOverAPZone = 14;
                        result = 4;
                        return result;
                    }
                }
            }

            // 获取最近的怪物
            if (MShare.g_APTagget != null)
            {
                if (MShare.g_APTagget.m_boDelActor || MShare.g_APTagget.m_boDeath)
                {
                    MShare.g_APTagget = null;
                }
            }
            if (MShare.GetTickCount() - MShare.g_dwSearchEnemyTick > 4000 || MShare.GetTickCount() - MShare.g_dwSearchEnemyTick > 300 && MShare.g_APTagget == null)
            {
                MShare.g_dwSearchEnemyTick = MShare.GetTickCount();
                MShare.g_APTagget = SearchTarget();
            }
            if (MShare.g_APTagget != null)
            {
                if (MShare.g_APTagget.m_boDelActor || MShare.g_APTagget.m_boDeath)
                {
                    MShare.g_APTagget = null;
                }
            }
            if (MShare.g_APTagget != null)
            {
                mobdistance = GetDis(MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY, MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY);
            }
            else
            {
                mobdistance = 100000;
            }
            // 获取最近的物品
            var itemDistance = 0;
            if (!bPcaketfull)
            {
                itemDistance = GetDropItemsDis();
            }
            else
            {
                MShare.g_AutoPicupItem = null;
            }
            if (itemDistance == 100000 && (mobdistance == 100000 || mobdistance == 0)) // 两者都没有发现
            {
                return 3; // 没有发现怪物或物品，随机走
            }
            if (itemDistance + 2 >= mobdistance) // 优先杀怪
            {
                result = 1; // 发现怪物
            }
            else
            {
                result = 2; // 发现物品
            }
            return result;
        }

        private bool AutoUseMagic(byte magicKey, TActor target, int nx = 0, int ny = 0)
        {
            var pcm = _robotClient.GetMagicByID(magicKey);
            if (pcm == null) return false;
            MShare.g_FocusCret = target;
            if (nx >= 0)
            {
                _robotClient.UseMagic(nx, ny, pcm, true);
            }
            else
            {
                _robotClient.UseMagic(target.m_nCurrX, target.m_nCurrY, pcm);
            }
            return true;
        }

        private int TargetCount(TActor target)
        {
            var result = 1;
            var rx = target.m_nCurrX + 1;
            var ry = target.m_nCurrY;
            var actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX + 1;
            ry = (short)(target.m_nCurrY + 1);
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX + 1;
            ry = (short)(target.m_nCurrY - 1);
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX - 1;
            ry = target.m_nCurrY;
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX - 1;
            ry = (short)(target.m_nCurrY + 1);
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX - 1;
            ry = (short)(target.m_nCurrY - 1);
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX;
            ry = (short)(target.m_nCurrY + 1);
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            rx = target.m_nCurrX;
            ry = (short)(target.m_nCurrY - 1);
            actor = _robotClient.g_PlayScene.FindActorXY(rx, ry);
            if (IsProperTarget(actor)) result++;
            return result;
        }

        private int TargetCount2(TActor target)
        {
            var result = 0;
            var wvar1 = _robotClient.g_PlayScene;
            for (var i = 0; i < wvar1.m_ActorList.Count; i++)
            {
                var actor = wvar1.m_ActorList[i];
                if (Math.Abs(actor.m_nCurrX - MShare.g_MySelf.m_nCurrX) < 6 ||
                    Math.Abs(actor.m_nCurrY - MShare.g_MySelf.m_nCurrY) < 6)
                    if (IsProperTarget(actor))
                        result++;
            }
            return result;
        }

        private int TargetCount3(TActor target)
        {
            var result = 0;
            var wvar1 = _robotClient.g_PlayScene;
            for (var i = 0; i < wvar1.m_ActorList.Count; i++)
            {
                var actor = wvar1.m_ActorList[i];
                if (Math.Abs(actor.m_nCurrX - MShare.g_MySelf.m_nCurrX) < 5 ||
                    Math.Abs(actor.m_nCurrY - MShare.g_MySelf.m_nCurrY) < 5)
                    if (IsProperTarget(actor))
                        result++;
            }

            return result;
        }

        public int TargetHumCount(TActor target)
        {
            var result = 0;
            var wvar1 = _robotClient.g_PlayScene;
            for (var i = 0; i < wvar1.m_ActorList.Count; i++)
            {
                var actor = wvar1.m_ActorList[i];
                if (Math.Abs(actor.m_nCurrX - MShare.g_MySelf.m_nCurrX) < 8 ||
                    Math.Abs(actor.m_nCurrY - MShare.g_MySelf.m_nCurrY) < 8)
                {
                    var b = actor != null && !actor.m_boDeath && (actor.m_btRace == 0 || actor.m_btIsHero == 1);
                    if (b) result++;
                }
            }

            return result;
        }

        public bool AttackTagget(TActor target)
        {
            int n;
            int m;
            int tdir;
            int magicKey;
            int i;
            int nTag;
            short nx = 0;
            short ny = 0;
            short nAbsX = 0;
            short nAbsY = 0;
            short nNx = 0;
            short nNy = 0;
            short nTx = 0;
            short nTy = 0;
            int nOldDc;
            var result = false;
            MShare.g_boAPAutoMove = false;
            MShare.g_nTagCount = 0;
            if (MShare.g_MySelf == null || MShare.g_MySelf.m_boDeath || MShare.g_APTagget == null || MShare.g_APTagget.m_boDeath)
            {
                return result;
            }
            switch (MShare.g_MySelf.m_btJob)
            {
                case 0:
                    if (MShare.g_SeriesSkillReady) _robotClient.SendFireSerieSkill();
                    if (MShare.g_gcTec[4] && (MShare.g_MySelf.m_nState & 0x00100000) == 0 && CanNextSpell())
                    {
                        if (MShare.g_MagicArr[31] != null)
                        {
                            _robotClient.UseMagic(MShare.SCREENWIDTH / 2, MShare.SCREENHEIGHT / 2, MShare.g_MagicArr[31]);
                            return result;
                        }
                    }
                    if (_robotClient.AttackTarget(MShare.g_APTagget))
                    {
                        return true;
                    }
                    break;
                case 1:
                    if (MShare.g_MySelf.m_Abil.Level < 7)
                    {
                        if (_robotClient.AttackTarget(MShare.g_APTagget))
                        {
                            result = true;
                        }
                        return result;
                    }
                    if (MShare.g_gcTec[4] && (MShare.g_MySelf.m_nState & 0x00100000) == 0 && CanNextSpell())
                    {
                        if (MShare.g_MagicArr[31] != null)
                        {
                            _robotClient.UseMagic(MShare.SCREENWIDTH / 2, MShare.SCREENHEIGHT / 2, MShare.g_MagicArr[31]);
                            return result;
                        }
                    }
                    if (MShare.g_SeriesSkillReady && MShare.g_MagicLockActor != null && !MShare.g_MagicLockActor.m_boDeath)
                    {
                        _robotClient.SendFireSerieSkill();
                    }
                    magicKey = 11;
                    nAbsX = (short)Math.Abs(MShare.g_MySelf.m_nCurrX - MShare.g_APTagget.m_nCurrX);
                    nAbsY = (short)Math.Abs(MShare.g_MySelf.m_nCurrY - MShare.g_APTagget.m_nCurrY);
                    if (nAbsX > 2 || nAbsY > 2)
                    {
                        if (nAbsX <= MShare.g_nMagicRange && nAbsY <= MShare.g_nMagicRange)
                        {
                            result = true;
                            MShare.g_sAPStr = $"怪物目标：{MShare.g_APTagget.m_sUserName} ({MShare.g_APTagget.m_nCurrX},{MShare.g_APTagget.m_nCurrY}) 正在使用魔法攻击";
                            if (_robotClient.CanNextAction() && _robotClient.ServerAcceptNextAction())
                                if (CanNextSpell())
                                {
                                    if (MShare.g_MagicArr[22] != null)
                                    {
                                        if (TargetCount3(MShare.g_APTagget) >= 15)
                                        {
                                            tdir = ClFunc.GetNextDirection(MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY, MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY);
                                            ClFunc.GetFrontPosition(MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY, tdir, ref nx, ref ny);
                                            ClFunc.GetFrontPosition(nx, ny, tdir, ref nx, ref ny);
                                            //if (robotClient.EventMan.GetEvent(nx, ny, Grobal2.ET_FIRE) == null)
                                            //{
                                            //    MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                            //    if (AutoUseMagic(22, MShare.g_APTagget, nx, ny))
                                            //    {
                                            //        return result;
                                            //    }
                                            //    result = false;
                                            //    return result;
                                            //}
                                        }
                                    }
                                    nOldDc = 3;
                                FFFF:
                                    if (MShare.g_MagicArr[10] != null)
                                    {
                                        tdir = ClFunc.GetNextDirection(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY);
                                        if (_robotClient.GetNextPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, 1, ref nNx, ref nNy))
                                        {
                                            _robotClient.GetNextPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, 8, ref nTx, ref nTy);
                                            if (_robotClient.CheckMagPassThrough(nNx, nNy, nTx, nTy, tdir) >= nOldDc)
                                            {
                                                MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                                magicKey = 10;
                                                goto AAAA;
                                            }
                                        }
                                    }
                                    if (MShare.g_MagicArr[9] != null)
                                    {
                                        tdir = ClFunc.GetNextDirection(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY);
                                        if (_robotClient.GetNextPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, 1, ref nNx, ref nNy))
                                        {
                                            _robotClient.GetNextPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, 5, ref nTx, ref nTy);
                                            if (_robotClient.CheckMagPassThrough(nNx, nNy, nTx, nTy, tdir) >= nOldDc)
                                            {
                                                MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                                magicKey = 9;
                                                goto AAAA;
                                            }
                                        }
                                    }
                                    if (MShare.m_btMagPassTh > 0)
                                    {
                                        MShare.m_btMagPassTh -= 1;
                                        nOldDc = 1;
                                        goto FFFF;
                                    }

                                    if (MShare.g_MagicArr[11] != null)
                                        magicKey = 11;
                                    else if (MShare.g_MagicArr[5] != null)
                                        magicKey = 5;
                                    else if (MShare.g_MagicArr[1] != null) magicKey = 1;
                                    MShare.g_nTagCount = TargetCount(MShare.g_APTagget);
                                    if (MShare.g_nTagCount >= 2)
                                    {
                                        if (new Random(7).Next() > 1)
                                        {
                                            if (MShare.g_MagicArr[58] != null && new Random(8).Next() > 1)
                                                magicKey = 58;
                                            else if (MShare.g_MagicArr[33] != null) magicKey = 33;
                                        }
                                        else if (MShare.g_MagicArr[47] != null)
                                        {
                                            magicKey = 47;
                                        }

                                        if (magicKey <= 11 && MShare.g_MagicArr[23] != null) magicKey = 23;
                                    }
                                    result = false;
                                    return result;
                                }
                        }
                        else
                        {
                            result = false;
                            return result;
                        }

                    }
                    else
                    {
                        if (nAbsX <= 1 && nAbsY <= 1)// 目标近身
                        {
                            if (CanNextSpell())
                            {
                                nTag = TargetCount(MShare.g_MySelf);
                                if (nTag >= 5)// 怪太多,强攻解围...
                                {
                                    goto DDDD;
                                    result = false;
                                    return result;// 躲避
                                }
                                if (nTag >= 4 && MShare.g_MagicArr[8] != null)
                                {
                                    // 比较勉强的抗拒...一般选择逃避
                                    result = true;
                                    MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                    if (AutoUseMagic(8, MShare.g_MySelf))
                                    {
                                        if (MShare.m_btMagPassTh <= 0) MShare.m_btMagPassTh += 1 + RandomNumber.GetInstance().Random(2);
                                        return result;
                                    }
                                }
                            }
                        }
                        tdir = ClFunc.GetNextDirection(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY);// 避怪
                        ClFunc.GetBackPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nx, ref ny);
                        nTag = 0;
                        while (true)
                        {
                            if (_robotClient.g_PlayScene.CanWalk(nx, ny)) break;
                            tdir++;
                            tdir = tdir % 8;
                            ClFunc.GetBackPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nx, ref ny);
                            nTag++;
                            if (nTag > 8) break;
                        }
                        if (_robotClient.g_PlayScene.CanWalk(nx, ny))
                        {
                            ClFunc.GetBackPosition2(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nTx, ref nTy);
                            // Map.CanMove(nTX, nTY)
                            if (_robotClient.g_PlayScene.CanWalk(nTx, nTy))
                            {
                                // DScreen.AddChatBoardString(Format('避怪2(%d:%d)...........', [nTX, nTY]), clBlue, clWhite);
                                MShare.g_nTargetX = nTx;
                                MShare.g_nTargetY = nTy;
                                MShare.g_ChrAction = TChrAction.caRun;
                                MShare.g_nMouseCurrX = nTx;
                                MShare.g_nMouseCurrY = nTy;
                                result = true;
                            }
                            else
                            {
                                // DScreen.AddChatBoardString(Format('避怪(%d:%d)...........', [nX, nY]), clBlue, clWhite);
                                MShare.g_nTargetX = nx;
                                MShare.g_nTargetY = ny;
                                MShare.g_ChrAction = TChrAction.caRun;
                                MShare.g_nMouseCurrX = nx;
                                MShare.g_nMouseCurrY = ny;
                                result = true;
                            }
                        }
                        else
                        {
                            // 强攻
                            // DScreen.AddChatBoardString('强攻...........', clBlue, clWhite);
                            goto DDDD;
                        }
                    }
                AAAA:
                    if (AutoUseMagic((byte)magicKey, MShare.g_APTagget))
                    {
                        return result;
                    }
                DDDD:
                    if (CanNextSpell())
                    {
                        magicKey = 0;
                        if (new Random(7).Next() > 1)
                        {
                            if (MShare.g_MagicArr[58] != null && new Random(8).Next() > 1)
                                magicKey = 58;
                            else if (MShare.g_MagicArr[33] != null) magicKey = 33;
                        }
                        else if (MShare.g_MagicArr[47] != null)
                        {
                            magicKey = 47;
                        }
                        if (magicKey <= 11 && MShare.g_MagicArr[23] != null) magicKey = 23;
                        if (magicKey > 0)
                        {
                            result = true;
                            goto AAAA;
                        }
                        if (MShare.g_MagicArr[11] != null)
                            magicKey = 11;
                        else if (MShare.g_MagicArr[5] != null)
                            magicKey = 5;
                        else if (MShare.g_MagicArr[1] != null) magicKey = 1;
                        if (magicKey > 0)
                        {
                            result = true;
                            goto AAAA;
                        }
                    }
                    break;
                case 2:
                    if (MShare.g_gcTec[4] && (MShare.g_MySelf.m_nState & 0x00100000) == 0 && CanNextSpell())
                    {
                        if (MShare.g_MagicArr[31] != null)
                        {
                            _robotClient.UseMagic(MShare.SCREENWIDTH / 2, MShare.SCREENHEIGHT / 2, MShare.g_MagicArr[31]);
                            return result;
                        }
                    }
                    n = 0;
                    if (MShare.g_UseItems[Grobal2.U_ARMRINGL].Item.StdMode == 25 && MShare.g_UseItems[Grobal2.U_ARMRINGL].Item.Shape != 6 && MShare.g_UseItems[Grobal2.U_ARMRINGL].Item.Name.IndexOf("药", StringComparison.Ordinal) > 0)
                    {
                        n++;
                    }
                    if (n == 0)
                    {
                        for (i = 6; i < MShare.MAXBAGITEMCL; i++)
                        {
                            if (MShare.g_ItemArr[i].Item.NeedIdentify < 4 && MShare.g_ItemArr[i].Item.StdMode == 25 && MShare.g_ItemArr[i].Item.Shape != 6 && MShare.g_ItemArr[i].Item.Name.IndexOf("药", StringComparison.Ordinal) > 0)
                            {
                                n++;
                                break;
                            }
                        }
                    }
                    if (n == 0)
                    {
                        if (MShare.GetTickCount() - _gHinttick1 > 60 * 1000)
                        {
                            _gHinttick1 = MShare.GetTickCount();
                            _robotClient.DScreen.AddChatBoardString("你的[药粉]已经用完，注意补充", ConsoleColor.Blue);
                        }
                    }

                    m = 0;
                    if (MShare.g_UseItems[Grobal2.U_ARMRINGL].Item.StdMode == 25 && MShare.g_UseItems[Grobal2.U_ARMRINGL].Item.Shape != 6 && MShare.g_UseItems[Grobal2.U_ARMRINGL].Item.Name.IndexOf("符", StringComparison.Ordinal) > 0)
                    {
                        m++;
                    }
                    if (m == 0)
                    {
                        for (i = 6; i < MShare.MAXBAGITEMCL; i++)
                        {
                            if (MShare.g_ItemArr[i].Item.NeedIdentify < 4 && MShare.g_ItemArr[i].Item.StdMode == 25 && MShare.g_ItemArr[i].Item.Shape != 6 && MShare.g_ItemArr[i].Item.Name.IndexOf("符", StringComparison.Ordinal) > 0)
                            {
                                m++;
                                break;
                            }
                        }
                    }
                    if (m == 0)
                    {
                        if (MShare.GetTickCount() - _gHinttick2 > 60 * 1000)
                        {
                            _gHinttick2 = MShare.GetTickCount();
                            _robotClient.DScreen.AddChatBoardString("你的[护身符]已经用完，注意补充", ConsoleColor.Blue);
                        }
                    }
                    if (MShare.GetTickCount() - MShare.m_dwRecallTick > 1000 * 6)
                    {
                        // 设置比较大时间,以便其他攻击...
                        MShare.m_dwRecallTick = MShare.GetTickCount();
                        if (MShare.g_MySelf.m_SlaveObject.Count == 0 && m > 0)
                        {
                            magicKey = 0;
                            if (MShare.g_MagicArr[55] != null)
                                magicKey = 55;
                            else if (MShare.g_MagicArr[30] != null)
                                magicKey = 30;
                            else if (MShare.g_MagicArr[17] != null)
                                magicKey = 17;

                            if (magicKey != 0)
                            {
                                result = true;
                                var pcm = _robotClient.GetMagicByID(magicKey);
                                if (pcm == null)
                                {
                                    result = false;
                                    return result;
                                }
                                MShare.g_FocusCret = null;
                                tdir = ClFunc.GetNextDirection(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY);
                                ClFunc.GetFrontPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nx, ref ny);
                                _robotClient.UseMagic(nx, ny, pcm, true);
                                return result;
                            }
                        }
                    }

                    if (MShare.GetTickCount() - MShare.m_dwSpellTick > 1000 * 5)
                    {
                        // 状态类魔法...
                        MShare.m_dwSpellTick = MShare.GetTickCount();
                        // MAGDEFENCEUP
                        if (MShare.g_MagicArr[14] != null && m > 0)
                        {
                            if ((MShare.g_MySelf.m_nState & (0x00200000)) == 0)
                            {
                                result = true;
                                if (AutoUseMagic(14, MShare.g_MySelf)) return result;
                            }
                        }
                        // Double DefenceUp
                        if (MShare.g_MagicArr[15] != null && m > 0)
                        {
                            if ((MShare.g_MySelf.m_nState & 0x00400000) == 0)
                            {
                                result = true;
                                if (AutoUseMagic(15, MShare.g_MySelf)) return result;
                            }
                        }
                        // Healling
                        if (MShare.g_MagicArr[2] != null)
                        {
                            if (HUtil32.Round(MShare.g_MySelf.m_Abil.HP / MShare.g_MySelf.m_Abil.MaxHP * 100) < 85)
                            {
                                result = true;
                                if (AutoUseMagic(2, MShare.g_MySelf)) return result;
                            }
                            if (MShare.g_MySelf.m_SlaveObject.Count > 0)
                            {
                                for (i = 0; i < MShare.g_MySelf.m_SlaveObject.Count; i++)
                                {
                                    if (MShare.g_MySelf.m_SlaveObject[i].m_boDeath) continue;
                                    if (MShare.g_MySelf.m_SlaveObject[i].m_Abil.HP != 0 && Math.Abs(MShare.g_MySelf.m_nCurrX - MShare.g_MySelf.m_SlaveObject[i].m_nCurrX + 2) <= MShare.g_nMagicRange
                                                                                        && Math.Abs(MShare.g_MySelf.m_nCurrY - MShare.g_MySelf.m_SlaveObject[i].m_nCurrY + 2) <= MShare.g_nMagicRange)
                                    {
                                        if (HUtil32.Round(MShare.g_MySelf.m_SlaveObject[i].m_Abil.HP / MShare.g_MySelf.m_SlaveObject[i].m_Abil.MaxHP * 100) <= 80)
                                        {
                                            result = true;
                                            if (AutoUseMagic(2, MShare.g_MySelf.m_SlaveObject[i])) return result;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (MShare.g_MySelf.m_Abil.Level < 18 || MShare.g_MagicArr[13] == null || n == 0 && m == 0)
                    {
                        goto CCCC;
                        if (_robotClient.AttackTarget(MShare.g_APTagget)) result = true;
                        return result;
                    }
                    if (MShare.g_SeriesSkillReady && MShare.g_MagicLockActor != null && !MShare.g_MagicLockActor.m_boDeath)
                    {
                        _robotClient.SendFireSerieSkill();
                    }
                    magicKey = 0;
                    nAbsX = (short)Math.Abs(MShare.g_MySelf.m_nCurrX - MShare.g_APTagget.m_nCurrX);
                    nAbsY = (short)Math.Abs(MShare.g_MySelf.m_nCurrY - MShare.g_APTagget.m_nCurrY);
                    if (nAbsX > 2 || nAbsY > 2)
                    {
                        // 需要快速检测类...
                        if (nAbsX <= MShare.g_nMagicRange && nAbsY <= MShare.g_nMagicRange)
                        {
                            result = true;
                            MShare.g_sAPStr = $"怪物目标：{MShare.g_APTagget.m_sUserName} ({MShare.g_APTagget.m_nCurrX},{MShare.g_APTagget.m_nCurrY}) 正在使用魔法攻击";
                            if (_robotClient.CanNextAction() && _robotClient.ServerAcceptNextAction())
                            {
                                goto EEEE;
                            }
                        }
                        else
                        {
                            result = false;
                            return result;
                        }
                    }
                    else
                    {
                        if (nAbsX <= 1 && nAbsY <= 1)// 目标近身
                            if (CanNextSpell())
                            {
                                nTag = TargetCount(MShare.g_MySelf);
                                if (nTag >= 5)// 怪太多,强攻解围...
                                    goto EEEE;
                                if (MShare.g_MagicArr[48] != null && nTag >= 3)
                                {
                                    // 有力的抗拒...不同于法师逃避
                                    result = true;
                                    MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                    if (AutoUseMagic(48, MShare.g_MySelf))
                                    {
                                        if (MShare.m_btMagPassTh <= 0) MShare.m_btMagPassTh += 1 + RandomNumber.GetInstance().Random(2);
                                        return result;
                                    }
                                }
                            }

                        tdir = ClFunc.GetNextDirection(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, MShare.g_APTagget.m_nCurrX, MShare.g_APTagget.m_nCurrY);
                        ClFunc.GetBackPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nx, ref ny); // 避怪
                        nTag = 0;
                        while (true)
                        {
                            if (_robotClient.g_PlayScene.CanWalk(nx, ny)) break;
                            tdir++;
                            tdir = tdir % 8;
                            ClFunc.GetBackPosition(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nx, ref ny);
                            nTag++;
                            if (nTag > 8) break;
                        }

                        if (_robotClient.g_PlayScene.CanWalk(nx, ny))
                        {
                            ClFunc.GetBackPosition2(MShare.g_MySelf.m_nCurrX, MShare.g_MySelf.m_nCurrY, tdir, ref nTx, ref nTy);
                            // Map.CanMove(nTX, nTY)
                            if (_robotClient.g_PlayScene.CanWalk(nTx, nTy))
                            {
                                // DScreen.AddChatBoardString(Format('避怪2(%d:%d)...........', [nTX, nTY]), clBlue, clWhite);
                                MShare.g_nTargetX = nTx;
                                MShare.g_nTargetY = nTy;
                                MShare.g_ChrAction = TChrAction.caRun;
                                MShare.g_nMouseCurrX = nTx;
                                MShare.g_nMouseCurrY = nTy;
                                result = true;
                            }
                            else
                            {
                                // DScreen.AddChatBoardString(Format('避怪(%d:%d)...........', [nX, nY]), clBlue, clWhite);
                                MShare.g_nTargetX = nx;
                                MShare.g_nTargetY = ny;
                                MShare.g_ChrAction = TChrAction.caRun;
                                MShare.g_nMouseCurrX = nx;
                                MShare.g_nMouseCurrY = ny;
                                result = true;
                            }
                        }
                        else
                        {
                            // 强攻
                            // DScreen.AddChatBoardString('强攻...........', clBlue, clWhite);
                            goto EEEE;
                        }
                    }
                EEEE:
                    if (CanNextSpell())
                    {
                        // DoubluSC
                        if (MShare.g_MagicArr[50] != null)
                            if (MShare.GetTickCount() - MShare.m_dwDoubluSCTick > 90 * 1000)
                            {
                                MShare.m_dwDoubluSCTick = MShare.GetTickCount();
                                MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                magicKey = 50;
                                goto BBBB;
                            }

                        // DECHEALTH & DAMAGEARMOR
                        if (MShare.GetTickCount() - MShare.m_dwPoisonTick > 3500)
                        {
                            MShare.m_dwPoisonTick = MShare.GetTickCount();
                            if (MShare.g_MagicArr[6] != null)
                            {
                                if ((MShare.g_APTagget.m_nState & (0x80000000)) == 0 || (MShare.g_APTagget.m_nState & (0x40000000)) == 0)
                                {
                                    MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                    magicKey = 6;
                                    goto BBBB;
                                }
                            }
                            if (MShare.g_MagicArr[18] != null && (MShare.g_MySelf.m_nState & (0x00800000)) == 0 && new Random(4).Next() == 0)
                            {
                                if (TargetCount2(MShare.g_MySelf) >= 7)
                                {
                                    MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                    magicKey = 18;
                                    goto BBBB;
                                }
                            }
                        }
                        if (MShare.g_MagicArr[13] != null || MShare.g_MagicArr[57] != null)
                        {
                            if (MShare.g_MagicArr[57] != null && (HUtil32.Round(MShare.g_MySelf.m_Abil.HP / MShare.g_MySelf.m_Abil.MaxHP * 100) < 80 && new Random(100 - HUtil32.Round(MShare.g_MySelf.m_Abil.HP / MShare.g_MySelf.m_Abil.MaxHP * 100)).Next() > 5 || new Random(10).Next() > 6))
                            {
                                MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                magicKey = 57;
                                goto BBBB;
                            }
                            if (MShare.g_MagicArr[13] != null)
                            {
                                MShare.m_dwTargetFocusTick = MShare.GetTickCount();
                                magicKey = 13;
                            }
                        }
                    BBBB:
                        if (magicKey > 0)
                        {
                            if (AutoUseMagic((byte)magicKey, MShare.g_APTagget)) return result;
                        }
                        else
                        {
                            result = false;
                            goto CCCC;
                        }
                    }
                CCCC:
                    if (MShare.GetTickCount() - MShare.m_dwSpellTick > 3000)
                    {
                        MShare.m_dwSpellTick = MShare.GetTickCount();
                        if (MShare.g_MagicArr[2] != null)
                        {
                            result = true;
                            if (HUtil32.Round(MShare.g_MySelf.m_Abil.HP / MShare.g_MySelf.m_Abil.MaxHP * 100) < 85)
                            {
                                if (AutoUseMagic(2, MShare.g_MySelf))
                                {
                                    return result;
                                }
                            }
                            for (i = 0; i < MShare.g_MySelf.m_SlaveObject.Count; i++)
                            {
                                if (MShare.g_MySelf.m_SlaveObject[i].m_boDeath) continue;
                                if (Math.Abs(MShare.g_MySelf.m_nCurrX - MShare.g_MySelf.m_SlaveObject[i].m_nCurrX + 2) <= MShare.g_nMagicRange && Math.Abs(MShare.g_MySelf.m_nCurrY - MShare.g_MySelf.m_SlaveObject[i].m_nCurrY + 2) <= MShare.g_nMagicRange)
                                {
                                    if (MShare.g_MySelf.m_SlaveObject[i].m_Abil.HP != 0 && HUtil32.Round(MShare.g_MySelf.m_SlaveObject[i].m_Abil.HP / MShare.g_MySelf.m_SlaveObject[i].m_Abil.MaxHP * 100) < 85)
                                    {
                                        if (AutoUseMagic(2, MShare.g_MySelf.m_SlaveObject[i]))
                                        {
                                            return result;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
            return result;
        }
    }
}