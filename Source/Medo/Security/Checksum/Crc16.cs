//Josip Medved <jmedved@jmedved.com>   www.medo64.com

//2008-06-07: Replaced ShiftRight function with right shift (>>) operator.
//            Implemented bit reversal via lookup table (http://graphics.stanford.edu/~seander/bithacks.html) and inlined byte bit reversal.
//            Append is not longer returning intermediate digest (performance reasons).
//2008-04-11: Cleaned code to match FxCop 1.36 beta 2.
//2008-01-05: Changed class in order to be CLS compliant.
//            Fixed CCITT and Z-modem calcultions.
//            Added resources.
//2007-10-31: New version.


namespace Medo.Security.Checksum {

    /// <summary>
    /// Computes hash using standard 16-bit CRC algorithm.
    /// </summary>
    public class Crc16 {

        private ushort[] _lookup = new ushort[256];
        private ushort _currDigest;
        private ushort _finalXorValue;
        private bool _reverseIn;
        private bool _reverseOut;


        /// <summary>
        /// Returns ARC implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetArc() { //BB3D
            return new Crc16(unchecked((short)0x8005), 0x0000, true, true, 0x0000);
        }

        /// <summary>
        /// Returns CCITT implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetCcitt() { //0x29B1
            return new Crc16(0x1021, unchecked((short)0xFFFF), false, false, 0x0000);
        }

        /// <summary>
        /// Returns IEEE 802.3 implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetIeee() { //0xBB3D
            return new Crc16(unchecked((short)0x8005), 0x0000, true, true, 0x0000);
        }

        /// <summary>
        /// Returns Kermit implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetKermit() { //0x2189
            return new Crc16(0x1021, 0x0000, true, true, 0x0000);
        }

        /// <summary>
        /// Returns X-25 implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetX25() { //0x906E
            return new Crc16(0x1021, unchecked((short)0xffff), true, true, unchecked((short)0xffff));
        }

        /// <summary>
        /// Returns X-Modem implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetXmodem() { //0x0C73
            return new Crc16(unchecked((short)0x8408), 0x0000, true, true, 0x0000);
        }

        /// <summary>
        /// Returns X-25 implementation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Calling the method results in different instances.")]
        public static Crc16 GetZmodem() { //0x31C3
            return new Crc16(0x1021, 0x0000, false, false, 0x0000);
        }


        /// <summary>
        /// Creates new instance using standard IEEE 802.3 implementation.
        /// </summary>
        public Crc16()
            : this(unchecked((short)0x8005), 0x0000, true, true, 0x0000) {
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        /// <param name="polynomial">Polynomial value.</param>
        /// <param name="initialValue">Starting digest.</param>
        /// <param name="reflectIn">If true, input byte is in reflected (LSB first) bit order.</param>
        /// <param name="reflectOut">If true, digest is in reflected (LSB first) bit order.</param>
        /// <param name="finalXorValue">Final XOR value.</param>
        /// <remarks>
        /// Name        Poly    Init    RefIn  RefOut  XorOut
        /// -------------------------------------------------
        /// ARC         0x8005  0x0000  true   true    0x0000
        /// CCITT       0x1021  0xffff  false  false   0x0000
        /// IEEE 802.3  0x8005  0x0000  true   true    0x0000
        /// Kermit      0x1021  0x0000  true   true    0x0000
        /// X-25        0x1021  0xffff  true   true    0xffff
        /// X-Modem     0x8408  0x0000  true   true    0x0000
        /// Z-Modem     0x1021  0x0000  false  false   0x0000
        /// 
        /// DNP         0x3D65
        /// IBM         0xC503
        /// </remarks>
        public Crc16(short polynomial, short initialValue, bool reflectIn, bool reflectOut, short finalXorValue) {
            this._currDigest = (ushort)initialValue;
            this._reverseIn = !reflectIn;
            this._reverseOut = !reflectOut;
            this._finalXorValue = (ushort)finalXorValue;

            ushort polynomialR = BitwiseReverse((ushort)polynomial);
            for (int i = 0; i < 256; i++) {
                ushort crcValue = (ushort)i;

                for (int j = 1; j <= 8; j++) {
                    if ((crcValue & 1) == 1) {
                        crcValue = (ushort)((crcValue >> 1) ^ polynomialR);
                    } else {
                        crcValue >>= 1;
                    }//if
                }//for j

                this._lookup[i] = crcValue;
            }//for i
        }

        /// <summary>
        /// Adds new data to digest and returns current digest.
        /// </summary>
        /// <param name="value">Data to add.</param>
        /// <exception cref="System.ArgumentNullException">Value cannot be null.</exception>
        public void Append(byte[] value) {
            if (value == null) { throw new System.ArgumentNullException("value", Resources.ExceptionValueCannotBeNull); }
            this.Append(value, 0, value.Length);
        }

        /// <summary>
        /// Adds new data to digest and returns current digest.
        /// </summary>
        /// <param name="value">Data to add.</param>
        /// <param name="index">A 32-bit integer that represents the index at which data begins.</param>
        /// <param name="length">A 32-bit integer that represents the number of elements.</param>
        /// <exception cref="System.ArgumentNullException">Value cannot be null.</exception>
        public void Append(byte[] value, int index, int length) {
            if (value == null) { throw new System.ArgumentNullException("value", Resources.ExceptionValueCannotBeNull); }
            for (int i = index; i < index + length; i++) {
                if (this._reverseIn) {
                    this._currDigest = (ushort)((this._currDigest >> 8) ^ this._lookup[(int)((this._currDigest & 0xff) ^ _lookupBitReverse[value[i]])]);
                } else {
                    this._currDigest = (ushort)((this._currDigest >> 8) ^ this._lookup[(int)((this._currDigest & 0xff) ^ value[i])]);
                }
            }
        }

        /// <summary>
        /// Adds new data to digest and returns current digest.
        /// </summary>
        /// <param name="value">Data to add.</param>
        /// <param name="useAsciiEncoding">If True, ASCII encoding is used instead of Unicode.</param>
        /// <exception cref="System.ArgumentNullException">Value cannot be null.</exception>
        public void Append(string value, bool useAsciiEncoding) {
            if (useAsciiEncoding) {
                this.Append(System.Text.ASCIIEncoding.ASCII.GetBytes(value));
            } else {
                this.Append(System.Text.UnicodeEncoding.Unicode.GetBytes(value));
            }//if
        }

        /// <summary>
        /// Gets current digest.
        /// </summary>
        public short Digest {
            get {
                if (this._reverseOut) {
                    return (short)(BitwiseReverse(this._currDigest) ^ this._finalXorValue);
                } else {
                    return (short)(this._currDigest ^ this._finalXorValue);
                }
            }
        }


        /// <summary>
        /// Computes CRC-16 (IEEE 802.3) digest from given data.
        /// </summary>
        /// <param name="value">Value.</param>
        public static short ComputeCrc(byte[] value) {
            Crc16 crc = Crc16.GetIeee();
            crc.Append(value);
            return crc.Digest;
        }

        /// <summary>
        /// Computes CRC-16 (IEEE 802.3) digest from given data.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <param name="index">A 32-bit integer that represents the index at which data begins.</param>
        /// <param name="length">A 32-bit integer that represents the number of elements.</param>
        public static short ComputeCrc(byte[] value, int index, int length) {
            Crc16 crc = Crc16.GetIeee();
            crc.Append(value, index, length);
            return crc.Digest;
        }

        /// <summary>
        /// Computes CRC-16 (IEEE 802.3) digest from given data.
        /// </summary>
        /// <param name="value">Unicode text for performing hash functions.</param>
        /// <param name="useAsciiEncoding">If True, ASCII encoding is used instead of Unicode.</param>
        public static short ComputeCrc(string value, bool useAsciiEncoding) {
            Crc16 crc = Crc16.GetIeee();
            crc.Append(value, useAsciiEncoding);
            return crc.Digest;
        }


        #region Bitwise

        private static readonly byte[] _lookupBitReverse = { 0x00, 0x80, 0x40, 0xC0, 0x20, 0xA0, 0x60, 0xE0, 0x10, 0x90, 0x50, 0xD0, 0x30, 0xB0, 0x70, 0xF0, 0x08, 0x88, 0x48, 0xC8, 0x28, 0xA8, 0x68, 0xE8, 0x18, 0x98, 0x58, 0xD8, 0x38, 0xB8, 0x78, 0xF8, 0x04, 0x84, 0x44, 0xC4, 0x24, 0xA4, 0x64, 0xE4, 0x14, 0x94, 0x54, 0xD4, 0x34, 0xB4, 0x74, 0xF4, 0x0C, 0x8C, 0x4C, 0xCC, 0x2C, 0xAC, 0x6C, 0xEC, 0x1C, 0x9C, 0x5C, 0xDC, 0x3C, 0xBC, 0x7C, 0xFC, 0x02, 0x82, 0x42, 0xC2, 0x22, 0xA2, 0x62, 0xE2, 0x12, 0x92, 0x52, 0xD2, 0x32, 0xB2, 0x72, 0xF2, 0x0A, 0x8A, 0x4A, 0xCA, 0x2A, 0xAA, 0x6A, 0xEA, 0x1A, 0x9A, 0x5A, 0xDA, 0x3A, 0xBA, 0x7A, 0xFA, 0x06, 0x86, 0x46, 0xC6, 0x26, 0xA6, 0x66, 0xE6, 0x16, 0x96, 0x56, 0xD6, 0x36, 0xB6, 0x76, 0xF6, 0x0E, 0x8E, 0x4E, 0xCE, 0x2E, 0xAE, 0x6E, 0xEE, 0x1E, 0x9E, 0x5E, 0xDE, 0x3E, 0xBE, 0x7E, 0xFE, 0x01, 0x81, 0x41, 0xC1, 0x21, 0xA1, 0x61, 0xE1, 0x11, 0x91, 0x51, 0xD1, 0x31, 0xB1, 0x71, 0xF1, 0x09, 0x89, 0x49, 0xC9, 0x29, 0xA9, 0x69, 0xE9, 0x19, 0x99, 0x59, 0xD9, 0x39, 0xB9, 0x79, 0xF9, 0x05, 0x85, 0x45, 0xC5, 0x25, 0xA5, 0x65, 0xE5, 0x15, 0x95, 0x55, 0xD5, 0x35, 0xB5, 0x75, 0xF5, 0x0D, 0x8D, 0x4D, 0xCD, 0x2D, 0xAD, 0x6D, 0xED, 0x1D, 0x9D, 0x5D, 0xDD, 0x3D, 0xBD, 0x7D, 0xFD, 0x03, 0x83, 0x43, 0xC3, 0x23, 0xA3, 0x63, 0xE3, 0x13, 0x93, 0x53, 0xD3, 0x33, 0xB3, 0x73, 0xF3, 0x0B, 0x8B, 0x4B, 0xCB, 0x2B, 0xAB, 0x6B, 0xEB, 0x1B, 0x9B, 0x5B, 0xDB, 0x3B, 0xBB, 0x7B, 0xFB, 0x07, 0x87, 0x47, 0xC7, 0x27, 0xA7, 0x67, 0xE7, 0x17, 0x97, 0x57, 0xD7, 0x37, 0xB7, 0x77, 0xF7, 0x0F, 0x8F, 0x4F, 0xCF, 0x2F, 0xAF, 0x6F, 0xEF, 0x1F, 0x9F, 0x5F, 0xDF, 0x3F, 0xBF, 0x7F, 0xFF };

        internal static ushort BitwiseReverse(ushort value) {
            return (ushort)((_lookupBitReverse[value & 0xff] << 8) | (_lookupBitReverse[(value >> 8)]));
        }

        #endregion


        private static class Resources {

            internal static string ExceptionValueCannotBeNull { get { return "Value cannot be null."; } }

        }

    }

}
