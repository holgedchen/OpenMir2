﻿using System.IO;

namespace M2Server
{
    public class TShortMessage
    {
        public short Ident;
        public short wMsg;

        public byte[] ToByte()
        {
            using (var memoryStream = new MemoryStream())
            {
                var backingStream = new BinaryWriter(memoryStream);

                backingStream.Write(Ident);
                backingStream.Write(wMsg);

                var stream = backingStream.BaseStream as MemoryStream;
                return stream.ToArray();
            }
        }
    }
}
