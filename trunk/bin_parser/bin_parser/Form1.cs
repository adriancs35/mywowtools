﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace bin_parser
{
    public partial class Form1 : Form
    {
        uint packet = 1;

        [Flags]
        enum Flags
        {
            flag01 = 0x00000001,
            flag02 = 0x00000002,
            flag03 = 0x00000004,
            flag04 = 0x00000008,
            flag05 = 0x00000010,
            flag06 = 0x00000020,
            flag07 = 0x00000040,
            flag08 = 0x00000080,
            flag09 = 0x00000100, // running
            flag10 = 0x00000200, // taxi
            flag11 = 0x00000400,
            flag12 = 0x00000800,
            flag13 = 0x00001000,
            flag14 = 0x00002000,
            flag15 = 0x00004000,
            flag16 = 0x00008000,
            flag17 = 0x00010000,
            flag18 = 0x00020000,
            flag19 = 0x00040000,
            flag20 = 0x00080000,
            flag21 = 0x00100000,
            flag22 = 0x00200000,
            flag23 = 0x00400000,
            flag24 = 0x00800000,
            flag25 = 0x01000000,
            flag26 = 0x02000000,
            flag27 = 0x04000000,
            flag28 = 0x08000000,
            flag29 = 0x10000000,
            flag30 = 0x20000000,
            flag31 = 0x40000000
        };

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string ifn = Directory.GetCurrentDirectory() + "\\" + "data.dump";

            if (!File.Exists(ifn))
            {
                MessageBox.Show("File " + ifn + " not found!");
                return;
            }

            GenericReader gr = new GenericReader(ifn, Encoding.ASCII);

            string error_log = "errors.txt";
            StreamWriter swe = new StreamWriter(error_log);

            string database_log = "data.txt";
            StreamWriter data = new StreamWriter(database_log);

            string ofn = "data_out.txt";
            StreamWriter sw = new StreamWriter(ofn);

            sw.AutoFlush = true;
            swe.AutoFlush = true;
            data.AutoFlush = true;

            try
            {
                while (gr.PeekChar() >= 0)
                {
                    uint result = ParseHeader(gr, sw, swe, data);
                    if (result == 0 || result == 1)
                        packet++;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.ToString());
            }

            sw.Close();
            swe.Close();
            data.Close();

            MessageBox.Show("Done!", "A9 parser", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly, false);
            //MessageBox.Show(br.BaseStream.Position.ToString() + " " + br.BaseStream.Length.ToString());

            gr.Close();
        }

        private uint ParseHeader(GenericReader gr, StreamWriter sw, StreamWriter swe, StreamWriter data)
        {
            StringBuilder sb = new StringBuilder();

            int datasize = gr.ReadInt32();

            //sb.AppendLine("Packet offset " + (gr.BaseStream.Position - 4).ToString("X2"));

            //sb.AppendLine("Packet number: " + packet);

            //sb.AppendLine("Data size " + datasize);

            if (!ParsePacket(gr, sb, swe, data, datasize))
            {
                if (sb.ToString().Length != 0)
                    sw.WriteLine(sb.ToString());
                return 1;
            }

            if(sb.ToString().Length != 0)
                sw.WriteLine(sb.ToString());

            return 0;
        }

        private bool ParsePacket(GenericReader gr, StringBuilder sb, StreamWriter swe, StreamWriter data, int datasize)
        {
            byte[] temp = gr.ReadBytes(datasize);
            MemoryStream ms = new MemoryStream(temp);
            GenericReader gr2 = new GenericReader(ms);

            ushort opcode = gr2.ReadUInt16();
            if (opcode != 0x00DD) // SMSG_MONSTER_MOVE
            {
                return false;
            }

            sb.AppendLine("Packet offset " + (gr.BaseStream.Position - 4).ToString("X2"));
            sb.AppendLine("Opcode " + opcode.ToString("X4"));

            ulong guid = gr2.ReadPackedGuid();
            sb.AppendLine("GUID " + guid.ToString("X16"));

            Coords3 coords = gr2.ReadCoords3();
            sb.AppendLine("Start point " + coords.GetCoords());

            uint time = gr2.ReadUInt32();
            sb.AppendLine("Time " + time);

            byte unk = gr2.ReadByte();
            sb.AppendLine("unk_byte " + unk);

            switch(unk)
            {
                case 0: // обычный пакет
                    break;
                case 1: // стоп, конец пакета...
                    sb.AppendLine("stop");
                    return true;
                case 3: // чей-то гуид, скорее всего таргета...
                    ulong target_guid = gr2.ReadUInt64();
                    sb.AppendLine("GUID unknown " + target_guid.ToString("X16"));
                    break;
                case 4: // похоже на ориентацию...
                    float orientation = gr2.ReadSingle();
                    sb.AppendLine("Orientation " + orientation.ToString().Replace(",", "."));
                    break;
                default:
                    swe.WriteLine("Error in position " + gr.BaseStream.Position.ToString("X2"));
                    swe.WriteLine("unknown unk " + unk);
                    break;
            }

            Flags flags = (Flags)gr2.ReadUInt32();
            sb.AppendLine("Flags " + flags);

            uint movetime = gr2.ReadUInt32();
            sb.AppendLine("MoveTime " + movetime);

            uint points = gr2.ReadUInt32();
            sb.AppendLine("Points " + points);

            if((flags & Flags.flag10) != 0) // 0x200
            {
                sb.AppendLine("Taxi");
                for (uint i = 0; i < points; i++)
                {
                    Coords3 path = gr2.ReadCoords3();
                    sb.AppendLine("Path point" + i + ": " + path.GetCoords());
                }
            }
            else
            {
                if((flags & Flags.flag09) == 0 && (flags & Flags.flag10) == 0 && flags != 0)
                {
                    swe.WriteLine("Unknown flags " + flags);
                }

                if((flags & Flags.flag09) != 0)
                    sb.AppendLine("Running");

                Coords3 end = gr2.ReadCoords3();
                sb.AppendLine("End point " + end.GetCoords());

                for (uint i = 0; i < (points - 1); i++)
                {
                    uint unk2 = gr2.ReadUInt32();
                    sb.AppendLine("Shift" + i + " " + unk2);
                }
            }

            return true;
        }
    }
}
