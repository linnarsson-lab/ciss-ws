using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using System.IO;

namespace Linnarsson.Dna
{
    public class QueueHandler
    {
        public static void WriteQueueFile(string queueFile, List<QueueRecord> queue)
        {
            StreamWriter writer = new StreamWriter(queueFile);
            WriteQueueFile(writer, queue);
        }

        public static void WriteQueueFile(StreamWriter writer, List<QueueRecord> queue)
        {
            foreach (QueueRecord record in queue)
                writer.WriteLine(record.ToString());
            writer.Close();
        }

        public static List<QueueRecord> ReadQueueFile(string queueFile)
        {
            Func<string[], QueueRecord> mapper = (arg) => new QueueRecord
            {
                RunFolder = arg[0],
                LaneNumbers = arg[1],
                ProjectFolder = arg[2],
                Species = arg[3],
                Build = arg[4],
                BarcodeSet = arg[5],
                Status = arg[6],
                Id = int.Parse(arg[7]),
                ResultSubPath = (arg.Length > 8)? arg[8] : ""
            };

            List<QueueRecord> allInQueue = new List<QueueRecord>();
            try
            {
                foreach (QueueRecord record in TabFileReader<QueueRecord>.Stream(queueFile, false, mapper))
                    allInQueue.Add(record);
            }
            catch (FileNotFoundException)
            {
                Console.Error.WriteLine("Could not find {0}. Trying to create it.", queueFile);
                File.Create(queueFile).Close();
            }
            catch (DirectoryNotFoundException)
            {
                Console.Error.WriteLine("Could not find path to {0}. Trying to create it.", queueFile);
                Directory.CreateDirectory(Path.GetDirectoryName(queueFile));
            }
            return allInQueue;
        }

        public static void UpdateQueueRecord(string queueFile, QueueRecord updateRec, string newStatus)
        {
            updateRec.SetStatus(newStatus);
            List<QueueRecord> records = ReadQueueFile(queueFile);
            for (int idx = 0; idx < records.Count; idx++)
            {
                if (records[idx].Id == updateRec.Id)
                    records[idx] = updateRec;
            }
            WriteQueueFile(queueFile, records);
        }

        public static void RemoveFromQueue(string queueFile, int recordId)
        {
            List<QueueRecord> records = ReadQueueFile(queueFile);
            records.RemoveAll((r) => (r.Id == recordId));
            WriteQueueFile(queueFile, records);
        }
    }

}
