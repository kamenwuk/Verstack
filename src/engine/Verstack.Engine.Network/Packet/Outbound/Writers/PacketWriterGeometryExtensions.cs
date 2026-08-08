using System.Runtime.CompilerServices;

namespace Verstack.Engine.Network.Packet.Outbound;

/// <summary>
/// Запись геометрических и идентификационных типов данных (UUID, Vectors).
/// </summary>
public static class PacketWriterGeometryExtensions
{
    extension(ref PacketStreamWriter streamWriter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteUuid(Guid value)
        {
            value.TryWriteBytes(streamWriter.FreeSpan, bigEndian: true, out _);
            streamWriter.Advance(16);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteVector2(int x, int z)
        {
            streamWriter.WriteInt(x);
            streamWriter.WriteInt(z);
            return ref streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteVector3(int x, int y, int z)
        {
            long value = ((long)x & 0x3FFFFFF) << 38;
            value |= ((long)z & 0x3FFFFFF) << 12;
            value |= (long)y & 0xFFF;
            streamWriter.WriteLong(value);
            return ref streamWriter;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteAngle(float degrees)
        {
            // Angle: 1 байт, шаг 1/256 оборота. signed/unsigned клиенту неважно — пишем как byte.
            // Нормализуем к [0,360), чтобы отрицательные yaw давали корректный байт.
            var normalized = degrees % 360f;
            if (normalized < 0f) normalized += 360f;
            streamWriter.WriteByte((byte)(normalized * 256f / 360f));
            return ref streamWriter;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref PacketStreamWriter WriteLpVec3(double x, double y, double z)
        {
            const double MAX_QUANTIZED = 32766.0;
            const long CONTINUATION_FLAG = 0x04L;
            const long SCALE_MASK = 0x03L;

            double maxAbs = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));

            // Особый случай: нулевая/пренебрежимо малая velocity → 1 байт.
            if (double.IsNaN(maxAbs) || maxAbs < 1.0 / MAX_QUANTIZED)
            {
                streamWriter.WriteByte(0);
                return ref streamWriter;
            }

            long scale = (long)Math.Ceiling(maxAbs);
            bool needContinuation = (scale & SCALE_MASK) != scale;

            // 2 младших бита scale + флаг continuation в битах 0–2.
            long packedScale = needContinuation ? (scale & SCALE_MASK) | CONTINUATION_FLAG : scale;

            long packed = packedScale
                          | (PackLpVec3Component(x / scale) << 3)
                          | (PackLpVec3Component(y / scale) << 18)
                          | (PackLpVec3Component(z / scale) << 33);

            // Split endianness: первые 2 байта little-endian, следующие 4 — big-endian.
            streamWriter.WriteByte((byte)packed);
            streamWriter.WriteByte((byte)(packed >> 8));
            streamWriter.WriteInt((int)(packed >> 16));

            if (needContinuation)
                streamWriter.WriteVarInt((int)(scale >> 2));

            return ref streamWriter;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static long PackLpVec3Component(double v)
                => (long)Math.Round((v * 0.5 + 0.5) * MAX_QUANTIZED);
        }
    }
}