﻿using GameSvr.Maps;

namespace GameSvr.Event.Events
{
    public class HolyCurtainEvent : EventInfo
    {
        public HolyCurtainEvent(Envirnoment Envir, short nX, short nY, byte nType, int nTime) : base(Envir, nX, nY, nType, nTime, true)
        {

        }
    }
}