using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TonieCreativeManager.Service
{
    public class Mp3Helper
    {
        /* mpeg audio header   */
        private struct mp3Header
        {
            public int sync;        /* Spezifies the Buffer for Sync from Start */
            public int offset; /* Offset bis zum ersten Sync */
            public int bitrate;     /* Bitrate of the mpeg file (bits / sec) */
            public int samplerate; /* Samplerate in Hz */
            public int framebytes;  /* Bytes per Frame */
            public int id;          /* 1 for mpeg1, 0 for mpeg2, 2 for mpeg2.5 */
            public int option;      /* 1 Layer III, 2 Layer II, 3 Layer I */
            public int prot;
            public int br_index;    /* Bitrate index for table */
            public int sr_index;    /* Samplerate index for table */
            public int pad;
            public int private_bit;
            public int mode;        /* stereo, joint stereo, dual scan, mono */
            public int mode_ext;
            public int cr;
            public int original;
            public int emphasis;
        }
        // samplerate for mpeg1 (id): {44100 48000 32000 1} depending on sr_index
        // samplerate for mpeg2 (id): {22050 24000 16000 1} depending on sr_index
        // samplerate for mpeg25(id): {11025 12000  8000 0} depending on sr_index
        // milliseconds per frame Layer I:   {8.707483f,  8.0f, 12.0f} depending on sr_index
        // milliseconds per frame Layer II:  {26.12245f, 24.0f, 36.0f} depending on sr_index
        // milliseconds per frame Layer III: {26.12245f, 24.0f, 36.0f} depending on sr_index
        private string mFilename;
        private int mLength;
        private mp3Header mInfo;

        private int mId3Track;
        private string mId3Album, mId3Artist, mId3Title;

        private bool mId3v1HasTag;
        private int mId3v1Track;
        private string mId3v1Title, mId3v1Artist, mId3v1Album, mId3v1Year, mId3v1Comment, mId3v1Genre;

        private bool mId3v2HasTag;
        private int mId3v2TagVersion, mId3v2TagLength;
        private int mId3v2PlayCounter, mId3v2Track, mId3v2Length, mId3v2UnknownFrames;
        private string mId3v2Encoder, mId3v2Link, mId3v2Copyright,
            mId3v2OriginalArtist, mId3v2Composer, mId3v2Genre,
            mId3v2Comment, mId3v2Year, mId3v2Album, mId3v2Artist, mId3v2Title,
            mId3v2Language, mId3v2MediaType, mId3v2Publisher,
            mId3v2DecodingSoftware;

        private static string[] id3genre =
    {
            "Blues", "Classic Rock", "Country", "Dance",
        "Disco", "Funk", "Grunge", "Hip-Hop", "Jazz",
        "Metal", "New Age", "Oldies", "Other", "Pop", "R&B",
        "Rap", "Reggae", "Rock", "Techno", "Industrial",
        "Alternative", "Ska", "Death Metal", "Pranks",
        "Soundtrack", "Euro-Techno", "Ambient", "Trip-Hop",
        "Vocal", "Jazz+Funk", "Fusion", "Trance",
        "Classical", "Instrumental", "Acid", "House",
        "Game", "Sound Clip", "Gospel", "Noise",
        "AlternRock", "Bass", "Soul", "Punk", "Space",
        "Meditative", "Instrumental Pop",
        "Instrumental Rock", "Ethnic", "Gothic", "Darkwave",
        "Techno-Industrial", "Electronic", "Pop-Folk",
        "Eurodance", "Dream", "Southern Rock", "Comedy",
        "Cult", "Gangsta", "Top 40", "Christian Rap",
        "Pop/Funk", "Jungle", "Native American", "Cabaret",
        "New Wave", "Psychadelic", "Rave", "Showtunes",
        "Trailer", "Lo-Fi", "Tribal", "Acid Punk",
        "Acid Jazz", "Polka", "Retro", "Musical",
        "Rock & Roll",
        "Hard Rock", "Folk", "Folk/Rock", "National Folk",
        "Swing", "Fast Fusion", "Bebob", "Latin", "Revival",
        "Celtic", "Bluegrass", "Avantgarde", "Gothic Rock",
        "Progressive Rock", "Psychedelic Rock",
        "Symphonic Rock", "Slow Rock", "Big Band",
        "Chorus", "Easy Listening", "Acoustic", "Humour",
        "Speech",
        "Chanson", "Opera", "Chamber Music", "Sonata",
        "Symphony", "Booty Bass", "Primus", "Porn Groove",
        "Satire", "Slow Jam", "Club", "Tango", "Samba",
        "Folklore", "Ballad", "Power Ballad",
        "Rhythmic Soul", "Freestyle", "Duet",
        "Punk Rock", "Drum Solo", "Acapella",
        "Euro-house", "Dance Hall", "", ""
    };

        static int[,] mp_br_tableL1 = //bitrate table for Layer I (mpeg, br_index)
            {
                    {0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0},	// mpeg2 + 2.5
			{0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0}}; // mpeg1

        static int[,] mp_br_table = //bitrate table for Layer II (mpeg, br_index)
            {
                    {0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0}, // mpeg2 + 2.5
			{0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0}}; // mpeg1

        static int[,] mp_br_tableL3 = //bitrate table for Layer III (mpeg, br_index)
            {
                    {0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0},	// mpeg2 + 2.5
			{0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0}}; // mpeg1

        static int[,] mp_sr20_table = //samplerate table (mpeg, sr_index)
            { { 441, 480, 320, -999 }, { 882, 960, 640, -999 } };

        static double[,] ms_p_f_table = //millisecounds per frame (Layer, sr_index)
            {
                    {26.12245f, 24.0f, 36.0f}, // Layer III
			{26.12245f, 24.0f, 36.0f}, // Layer II
			{8.707483f,  8.0f, 12.0f}}; // Layer I

        const int INFO_BUFFER = 0x2000;

#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
        public Mp3Helper()
        {
            Initialise();
        }
        public Mp3Helper(string filename)
        {
            Initialise();
            mFilename = filename;
            ReadInfo();
        }
#pragma warning restore CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.

        public static async Task<int> Mp3FileLengthAsync(string filename)
        {
            await Task.Delay(0);
            var h = new Mp3Helper(filename);
            return h.Length;
        }

        private void Initialise()
        {
            mInfo = new mp3Header();
            mLength = 0;

            mId3Track = 0;
            mId3Album = "";
            mId3Artist = "";
            mId3Title = "";

            mId3v1HasTag = false;
            mId3v1Track = 0;
            mId3v1Title = "";
            mId3v1Artist = "";
            mId3v1Album = "";
            mId3v1Year = "";
            mId3v1Comment = "";
            mId3v1Genre = "";

            mId3v2HasTag = false;
            mId3v2TagVersion = 0;
            mId3v2PlayCounter = 0;
            mId3v2Track = 0;
            mId3v2Length = 0;
            mId3v2UnknownFrames = 0;
            mId3v2Encoder = "";
            mId3v2Link = "";
            mId3v2Copyright = "";
            mId3v2OriginalArtist = "";
            mId3v2Composer = "";
            mId3v2Genre = "";
            mId3v2Comment = "";
            mId3v2Year = "";
            mId3v2Album = "";
            mId3v2Artist = "";
            mId3v2Title = "";
            mId3v2Language = "";
            mId3v2MediaType = "";
            mId3v2Publisher = "";
            mId3v2DecodingSoftware = "";
        }

        public string Filename { get { return mFilename; } set { mFilename = value; ReadInfo(); } }
        public int Length { get { return mLength; } }
        public int BitRate { get { return mInfo.bitrate; } }
        public int SampleRate { get { return mInfo.samplerate; } }

        public int Id3Track { get { return mId3Track; } }
        public string Id3Album { get { return mId3Album; } }
        public string Id3Artist { get { return mId3Artist; } }
        public string Id3Title { get { return mId3Title; } }

        public bool Id3v1HasTag { get { return mId3v1HasTag; } }

        public bool Id3v2HasTag { get { return mId3v2HasTag; } }
        public int Id3v2TagVersion { get { return mId3v2TagVersion; } }
        public int Id3v2PlayCounter { get { return mId3v2PlayCounter; } }
        public int Id3v2Length { get { return mId3v2Length; } }
        public int Id3v2UnknownFrames { get { return mId3v2UnknownFrames; } }
        public string Id3v2Encoder { get { return mId3v2Encoder; } }
        public string Id3v2Link { get { return mId3v2Link; } }
        public string Id3v2Copyright { get { return mId3v2Copyright; } }
        public string Id3v2OriginalArtist { get { return mId3v2OriginalArtist; } }
        public string Id3v2Composer { get { return mId3v2Composer; } }
        public string Id3v2Genre { get { return mId3v2Genre; } }
        public string Id3v2Comment { get { return mId3v2Comment; } }
        public string Id3v2Year { get { return mId3v2Year; } }
        public string Id3v2Album { get { return mId3v2Album; } }
        public string Id3v2Artist { get { return mId3v2Artist; } }
        public string Id3v2Title { get { return mId3v2Title; } }
        public string Id3v2Language { get { return mId3v2Language; } }
        public string Id3v2MediaType { get { return mId3v2MediaType; } }
        public string Id3v2Publisher { get { return mId3v2Publisher; } }
        public string Id3v2DecodingSoftware { get { return mId3v2DecodingSoftware; } }

        private void ReadInfo()
        {
            FileStream file = null;
            byte[] buf = new byte[INFO_BUFFER];
            string bufstr;
            int read;

            Initialise();
            try
            {
                CopyFileInfo(); //Generate Information out of Filename
                file = new FileStream(mFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                read = file.Read((byte[])buf, 0, INFO_BUFFER);
                mId3v2HasTag = CopyID3V2(ref buf);
                ReadHeader(ref buf);
                file.Seek(-128, SeekOrigin.End);
                read = file.Read(buf, 0, 128);
                mId3v1HasTag = buf[0] == 'T' && buf[1] == 'A' && buf[2] == 'G';
                if (mId3v1HasTag)
                {
                    bufstr = System.Text.ASCIIEncoding.ASCII.GetString(buf, 0, read);
                    CopyId3Tag(ref bufstr);
                    mLength = (int)System.Math.Floor(((file.Length - mInfo.sync - mInfo.offset - 128) / mInfo.framebytes * ms_p_f_table[mInfo.option - 1, mInfo.sr_index] / 1000));
                }
                else
                    mLength = (int)System.Math.Floor(((file.Length - mInfo.sync - mInfo.offset) / mInfo.framebytes * ms_p_f_table[mInfo.option - 1, mInfo.sr_index] / 1000));
            }
            catch
            {
            }
            finally
            {
                if (file != null) file.Close();
            }
        }

        private void CopyFileInfo()
        {
            int i, j;

            i = mFilename.LastIndexOf("\\");
            j = mFilename.Substring(0, i - 1).LastIndexOf("\\");
            if (j >= 0) mId3Album = mFilename.Substring(j + 1, i - j - 1);
            j = mFilename.IndexOfAny(" -.".ToCharArray(), i);
            if (j - i <= 5)
            {
                double Track;
                Double.TryParse(mFilename.Substring(i + 1, j - i - 1), System.Globalization.NumberStyles.Integer, null, out Track);
                mId3Track = (int)Track;
                i = j;
            }
            j = mFilename.IndexOf(" - ", i);
            if (j >= 0)
            {
                mId3Artist = mFilename.Substring(i + 1, j - i - 1);
                mId3Title = mFilename.Substring(j + 3);
            }
            else
                mId3Title = mFilename.Substring(i + 1);

            if (mId3Title.LastIndexOf(".") > 0)
                mId3Title = mId3Title.Substring(0, mId3Title.LastIndexOf("."));
            else
                mId3Title = "";
        }

        private void CopyId3Tag(ref string buffer)
        {

            mId3v1Title = buffer.Substring(3, 30).TrimEnd();
            if (mId3v1Title.IndexOf("\0") >= 0) mId3v1Title = mId3v1Title.Substring(0, mId3v1Title.IndexOf("\0"));
            mId3v1Artist = buffer.Substring(33, 30).TrimEnd();
            if (mId3v1Artist.IndexOf("\0") >= 0) mId3v1Artist = mId3v1Artist.Substring(0, mId3v1Artist.IndexOf("\0"));
            mId3v1Album = buffer.Substring(63, 30).TrimEnd();
            if (mId3v1Album.IndexOf("\0") >= 0) mId3v1Album = mId3v1Album.Substring(0, mId3v1Album.IndexOf("\0"));
            mId3v1Year = buffer.Substring(93, 4).TrimEnd();
            if (mId3v1Year.IndexOf("\0") >= 0) mId3v1Year = mId3v1Year.Substring(0, mId3v1Year.IndexOf("\0"));
            if (buffer[125] == '\0')
            {
                mId3v1Comment = buffer.Substring(97, 28).TrimEnd();
                mId3v1Track = buffer[126];
            }
            else
                mId3v1Comment = buffer.Substring(97, 30).TrimEnd();
            if (mId3v1Comment.IndexOf("\0") >= 0) mId3v1Comment = mId3v1Comment.Substring(0, mId3v1Comment.IndexOf("\0"));
            mId3v1Genre = id3genre[buffer[127]];
        }

        private bool CopyID3V2(ref byte[] buffer)
        {
            int id3Tag, id3Len, id3Version, pos;
            bool id3ExtendedHeader;
            string bufstr = System.Text.ASCIIEncoding.ASCII.GetString(buffer, 0, buffer.GetLength(0));


            id3Tag = bufstr.IndexOf("ID3");
            if (id3Tag == -1 || id3Tag > buffer.Length - 10) return false;
            id3Version = buffer[3] * 10 + buffer[4];
            if (id3Version < 20 || id3Version >= 50) return false;
            id3Len = (buffer[6] << 21) + (buffer[7] << 14) + (buffer[8] << 7) + buffer[9];
            id3ExtendedHeader = (buffer[5] & 0x20) == 0x20;
            if (id3ExtendedHeader == true)
                pos = id3Tag + 9;
            else
                pos = id3Tag + 10;
            mId3v2TagLength = pos + id3Len;

            while (true)
            {
                string frameName = "", frameFlags = "", frame = "";
                int frameLen, i;
                if (mId3v2TagLength - pos < 11) break;
                frameName = bufstr.Substring(pos, 4);
                if (frameName[3] == '\0') frameName = frameName.Substring(0, 3);
                //Frame-Header auslesen
                if (bufstr.Substring(pos, 3) == "3DI") break;
                if (frameName.Length == 4)
                {
                    frameLen = (buffer[pos + 4] << 24) + (buffer[pos + 5] << 16) + (buffer[pos + 6] << 8) + buffer[pos + 7];
                    if (frameLen == 0 || frameLen > 100) break;
                    frameFlags = bufstr.Substring(pos + 7, 2);
                    frame = bufstr.Substring(pos + 10, frameLen);
                    for (i = 0; i < frame.Length; i++)
                    {
                        if (frame[i] != '\0') break;
                    }
                    if (i > 0) frame = frame.Substring(i);
                    pos += 10 + frameLen;
                }
                else
                {
                    //Frame-Header auslesen
                    frameLen = (buffer[pos + 3] << 24) + (buffer[pos + 4] << 16) + (buffer[pos + 5] << 8) + buffer[pos + 6];
                    if (frameLen == 0) break;
                    frame = bufstr.Substring(pos + 7, frameLen);
                    if (frame[0] == '\0') frame = frame.Substring(1);
                    pos += 7 + frameLen;
                }
                if (frame.IndexOf("\0") >= 0) frame = frame.Substring(0, frame.IndexOf("\0"));
                switch (frameName)
                {
                    case "PCNT":
                        int.TryParse(frame, out mId3v2PlayCounter);
                        break;
                    case "TRCK":
                    case "TRK":
                        int.TryParse(frame, out mId3v2Track);
                        break;
                    case "TENC":
                    case "TEN":
                        mId3v2Encoder = frame;
                        break;
                    case "WXXX":
                        mId3v2Link = frame;
                        break;
                    case "TCOP":
                    case "TCR":
                        mId3v2Copyright = frame;
                        break;
                    case "TOPE":
                    case "TOA":
                        mId3v2OriginalArtist = frame;
                        break;
                    case "TCOM":
                    case "TCM":
                        mId3v2Composer = frame;
                        break;
                    case "TCON":
                    case "TCO":
                        mId3v2Genre = frame;
                        break;
                    case "COMM":
                    case "COM":
                        mId3v2Comment = frame;
                        break;
                    case "TYER":
                    case "TYE":
                        mId3v2Year = frame;
                        break;
                    case "TALB":
                    case "TAL":
                        mId3v2Album = frame;
                        break;
                    case "TPE1":
                    case "TP1":
                        mId3v2Artist = frame;
                        break;
                    case "TIT2":
                    case "TT2":
                        mId3v2Title = frame;
                        break;
                    case "TLAN":
                    case "TLA":
                        mId3v2Language = frame;
                        break;
                    case "TLEN":
                        int.TryParse(frame, out mId3v2Length);
                        break;
                    case "TMED":
                    case "TMT":
                        mId3v2MediaType = frame;
                        break;
                    case "TPUB":
                        mId3v2Publisher = frame;
                        break;
                    case "TSSE":
                    case "TSS":
                        mId3v2DecodingSoftware = frame;
                        break;
                    default:
                        mId3v2UnknownFrames += 1;
                        break;
                }
            }
            return true;
        }

        private bool ReadHeader(ref byte[] buffer)
        {
            int pos = 0;
            try
            {
                mInfo.offset = 0;
                while (!(buffer[pos] == 0xFF && (buffer[pos + 1] & 0xE0) == 0xE0) && pos <= buffer.GetLength(0) - 3) pos++;
                if (pos > buffer.GetLength(0) - 3) return false;
                mInfo.offset = pos;

                mInfo.id = (buffer[pos + 1] & 0x08) >> 3;
                if ((buffer[pos + 1] & 0xF0) != 0xF0) mInfo.id = 2;
                mInfo.option = (buffer[pos + 1] & 0x06) >> 1;
                mInfo.prot = (buffer[pos + 1] & 0x01);

                mInfo.br_index = (buffer[pos + 2] & 0xF0) >> 4;
                mInfo.sr_index = (buffer[pos + 2] & 0x0C) >> 2;
                mInfo.pad = (buffer[pos + 2] & 0x02) >> 1;
                mInfo.private_bit = (buffer[pos + 2] & 0x01);

                mInfo.mode = (buffer[pos + 3] & 0xC0) >> 6;
                mInfo.mode_ext = (buffer[pos + 3] & 0x30) >> 4;
                mInfo.cr = (buffer[pos + 3] & 0x08) >> 3;
                mInfo.original = (buffer[pos + 3] & 0x04) >> 2;
                mInfo.emphasis = (buffer[pos + 3] & 0x03);

                GetFrameBytes();
                if (mInfo.framebytes == 0) GetSync(ref buffer);
                GetBitRate();
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void GetFrameBytes()
        {
            int id = (mInfo.id == 2 ? 0 : mInfo.id);
            if (id == 0)
                mInfo.samplerate = mp_sr20_table[id, mInfo.sr_index] * 100;
            else
                mInfo.samplerate = mp_sr20_table[id, mInfo.sr_index] * 50;

            if (mInfo.option < 1) return;
            if (mInfo.option > 3) return;
            if (mInfo.br_index == 0) return; // free format

            // Layer III
            if (mInfo.option == 1)
            {
                if (mInfo.id == 1) // mpeg1
                    mInfo.framebytes = 2880 * mp_br_tableL3[id, mInfo.br_index]
                        / mp_sr20_table[id, mInfo.sr_index];
                else if (mInfo.id == 0) // mpeg2
                    mInfo.framebytes = 1440 * mp_br_tableL3[id, mInfo.br_index]
                        / mp_sr20_table[id, mInfo.sr_index];
                else // mpeg2.5
                    mInfo.framebytes = 2880 * mp_br_tableL3[id, mInfo.br_index]
                        / mp_sr20_table[id, mInfo.sr_index];
            }

            // Layer II
            else if (mInfo.option == 2)
                mInfo.framebytes = 2880 * mp_br_table[id, mInfo.br_index]
                    / mp_sr20_table[id, mInfo.sr_index];

            // Layer I
            else if (mInfo.option == 3)
                mInfo.framebytes = 960 * mp_br_tableL1[id, mInfo.br_index]
                    / mp_sr20_table[id, mInfo.sr_index];
        }

        private void GetSync(ref byte[] buffer)
        {
            int padbytes;
            int pos = 0;

            mInfo.sync = 24;
            if (mInfo.option == 3) padbytes = 4; else padbytes = 1;
            pos = mInfo.offset + mInfo.sync;

            while (true)
            {
                while (!(buffer[pos] == buffer[mInfo.offset] && buffer[pos + 1] == buffer[mInfo.offset + 1])
                    && pos <= buffer.GetLength(0) - 4) { mInfo.sync++; pos++; }

                if (pos > buffer.GetLength(0) - 4) return;

                // Syncronisation Test
                mInfo.sync -= mInfo.pad * padbytes;
                {
                    int pad, i;
                    for (i = mInfo.offset; i < buffer.GetLength(0); i += mInfo.sync)
                    {
                        pad = padbytes * ((buffer[i + 2] & 0x02) >> 1);
                        i += pad;
                        if (i > buffer.GetLength(0) - 4) break;
                        if (buffer[mInfo.offset] != buffer[i] || buffer[mInfo.offset + 1] != buffer[i + 1]) break;
                    }
                    if (i > buffer.GetLength(0) - 4)
                    {
                        mInfo.framebytes = mInfo.sync;
                        return;
                    }
                }
                mInfo.sync += mInfo.pad * padbytes + 1;
                pos += mInfo.offset + mInfo.sync;
            }
        }

        private void GetBitRate()
        {
            int id = (mInfo.id == 2 ? 0 : mInfo.id);

            // Layer III 
            if (mInfo.option == 1)
            {
                if (mInfo.br_index > 0)
                    mInfo.bitrate = mp_br_tableL3[id, mInfo.br_index];
                else
                {
                    if (mInfo.id == 1) // mpeg1
                        mInfo.bitrate = mInfo.framebytes * mp_sr20_table[id, mInfo.sr_index] / (144 * 20);
                    else if (mInfo.id == 0) // mpeg2
                        mInfo.bitrate = mInfo.framebytes * mp_sr20_table[id, mInfo.sr_index] / (72 * 20);
                    else // mpeg 2.5
                        mInfo.bitrate = (int)(0.5 * mInfo.framebytes * mp_sr20_table[id, mInfo.sr_index] / (72 * 20));
                }
            }

            // Layer II 
            else if (mInfo.option == 2)
            {
                if (mInfo.br_index > 0)
                    mInfo.bitrate = mp_br_table[id, mInfo.br_index];
                else
                    mInfo.bitrate = mInfo.framebytes * mp_sr20_table[id, mInfo.sr_index] / (144 * 20);
            }

            // Layer I
            else if (mInfo.option == 3)
            {
                if (mInfo.br_index > 0)
                    mInfo.bitrate = mp_br_tableL1[id, mInfo.br_index];
                else
                    mInfo.bitrate = mInfo.framebytes * mp_sr20_table[id, mInfo.sr_index] / (48 * 20);
            }
        }

        public void RenameFromID3()
        {
            string filename = mFilename;
            if (mId3v1HasTag)
                filename = mId3v1Track.ToString("00") + " " + mId3v1Artist.ToString() + " - " + mId3v1Title.ToString() + ".mp3";
            else
                if (mId3v2HasTag)
                filename = mId3v2Track.ToString("00") + " " + mId3v2Artist.ToString() + " - " + mId3v2Title.ToString() + ".mp3";
            try
            {
                System.IO.FileInfo f = new FileInfo(this.mFilename);
                f.MoveTo(filename);
                mFilename = f.FullName;
            }
            catch
            {
            }
        }

        public void RemoveTags(bool Id3v1, bool Id3v2)
        {
            if (Id3v1 && mId3v1HasTag)
            {
                FileStream f = new FileStream(mFilename, FileMode.Open, FileAccess.ReadWrite);
                f.SetLength(f.Length - 128);
                mId3v1HasTag = false;
                f.Close();
            }
            if (Id3v2 && mId3v2HasTag)
            {
                long inpPtr, outPtr;
                byte[] buf = new byte[0x10000];
                FileStream f = new FileStream(mFilename, FileMode.Open, FileAccess.ReadWrite);
                inpPtr = mId3v2TagLength;
                outPtr = 0;

                while (inpPtr < f.Length)
                {
                    int i;
                    f.Seek(inpPtr, SeekOrigin.Begin);
                    i = f.Read(buf, 0, buf.GetLength(0));
                    f.Seek(outPtr, SeekOrigin.Begin);
                    f.Write(buf, 0, i);
                    inpPtr += i;
                    outPtr += i;
                }
                f.SetLength(f.Length - mId3v2TagLength);
                f.Close();
                mId3v2HasTag = false;
            }
        }

    }
}
