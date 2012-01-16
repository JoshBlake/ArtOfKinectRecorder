using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace ArtofKinect.Common
{
    public class WavWriter
    {
        #region Fields

        const int RIFF_HEADER_SIZE = 20;
        const string RIFF_HEADER_TAG = "RIFF";
        const int WAVEFORMATEX_SIZE = 18; // native sizeof(WAVEFORMATEX)
        const int DATA_HEADER_SIZE = 8;
        const string DATA_HEADER_TAG = "data";
        const int FULL_HEADER_SIZE = RIFF_HEADER_SIZE + WAVEFORMATEX_SIZE + DATA_HEADER_SIZE;

        #region WAVEFORMATEX
        
        struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        #endregion

        #endregion

        #region Public Static Methods
        
        /// <summary>
        /// A bare bones WAV file header writer
        /// </summary>        
        public static void WriteWavHeader(Stream stream)
        {
            // Data length to be fixed up later
            int dataLength = 0;

            //We need to use a memory stream because the BinaryWriter will close the underlying stream when it is closed
            using (MemoryStream memStream = new MemoryStream(64))
            {
                WAVEFORMATEX format = new WAVEFORMATEX()
                {
                    wFormatTag = 1,
                    nChannels = 1,
                    nSamplesPerSec = 16000,
                    nAvgBytesPerSec = 32000,
                    nBlockAlign = 2,
                    wBitsPerSample = 16,
                    cbSize = 0
                };

                using (var bw = new BinaryWriter(memStream))
                {
                    //RIFF header
                    WriteHeaderString(memStream, RIFF_HEADER_TAG);
                    bw.Write(dataLength + FULL_HEADER_SIZE - 8); //File size - 8
                    WriteHeaderString(memStream, "WAVE");
                    WriteHeaderString(memStream, "fmt ");
                    bw.Write(WAVEFORMATEX_SIZE);

                    //WAVEFORMATEX
                    bw.Write(format.wFormatTag);
                    bw.Write(format.nChannels);
                    bw.Write(format.nSamplesPerSec);
                    bw.Write(format.nAvgBytesPerSec);
                    bw.Write(format.nBlockAlign);
                    bw.Write(format.wBitsPerSample);
                    bw.Write(format.cbSize);

                    //data header
                    WriteHeaderString(memStream, DATA_HEADER_TAG);
                    bw.Write(dataLength);
                    memStream.WriteTo(stream);
                }
            }
        }

        public static void UpdateDataLength(Stream stream, int dataLength)
        {
            using (var bw = new BinaryWriter(stream))
            {
                // Write file size - 8 to riff header
                bw.Seek(RIFF_HEADER_TAG.Length, SeekOrigin.Begin);
                bw.Write(dataLength + FULL_HEADER_SIZE - 8);

                // Write data size to data header
                bw.Seek(FULL_HEADER_SIZE - 4, SeekOrigin.Begin);
                bw.Write(dataLength);
            }
        }

        static void WriteHeaderString(Stream stream, string s)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(s);
            Debug.Assert(bytes.Length == s.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        #endregion
    }
}
