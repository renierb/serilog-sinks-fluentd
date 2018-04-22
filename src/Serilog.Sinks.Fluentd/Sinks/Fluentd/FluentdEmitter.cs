using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MsgPack;
using MsgPack.Serialization;

namespace Serilog.Sinks.Fluentd
{
    internal class FluentdEmitter
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        private readonly Packer _packer;
        private readonly SerializationContext _serializationContext;

        public FluentdEmitter(Stream stream)
        {
            _serializationContext = new SerializationContext(PackerCompatibilityOptions.PackBinaryAsRaw);
            _serializationContext.Serializers.Register(new OrdinaryDictionarySerializer());
            _packer = Packer.Create(stream);
        }

        public void Emit(DateTime timestamp, string tag, IDictionary<string, object> data)
        {
            _packer.PackArrayHeader(3);
            _packer.PackString(tag, Encoding.UTF8);
            _packer.PackExtendedTypeValue(0xd7, GetEventTimeBytes(timestamp));
            _packer.Pack(data, _serializationContext);
        }

        /// <summary>
        /// https://github.com/fluent/fluentd/wiki/Forward-Protocol-Specification-v0#eventtime-ext-format
        /// +----+----+----+----+----+----+----+----+----+----+
        /// |  1 |  2 |  3 |  4 |  5 |  6 |  7 |  8 |  9 | 10 |
        /// +----+----+----+----+----+----+----+----+----+----+
        /// | C7 | 00 | second from epoch |     nanosecond    |
        /// +----+----+----+----+----+----+----+----+----+----+
        /// |ext |type| 32bits integer BE | 32bits integer BE |
        /// +----+----+----+----+----+----+----+----+----+----+
        /// </summary>
        private static byte[] GetEventTimeBytes(DateTime timestamp)
        {
            var epochTimeSpan = timestamp.ToUniversalTime().Subtract(UnixEpoch);

            var bytes = new byte[9];
            Buffer.BlockCopy(GetEventTimeSecondsFromEpoch(epochTimeSpan), 0, bytes, 1, 4);
            Buffer.BlockCopy(GetEventTimeNanoseconds(epochTimeSpan), 0, bytes, 5, 4);
            return bytes;
        }

        private static byte[] GetEventTimeSecondsFromEpoch(TimeSpan timeSpan)
        {
            return BitConverter.GetBytes((uint) timeSpan.TotalSeconds);
        }
        
        private static byte[] GetEventTimeNanoseconds(TimeSpan timeSpan)
        {
            return BitConverter.GetBytes((uint) timeSpan.Milliseconds * 1000u);
        }
    }
}
