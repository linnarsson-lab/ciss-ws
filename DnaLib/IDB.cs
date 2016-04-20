using System;
using System.Collections.Generic;
using Linnarsson.C1;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Calls related to sample metadata, but not expression or wiggle plots
    /// </summary>
    public interface IDB
    {
        void AddToBackupQueue(string readFile, int priority);
        void GetCellAnnotations(string chipid, out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes);
        void InsertOrUpdateCell(Cell c);
        void InsertOrUpdateCellAnnotation(CellAnnotation ca);
        Cell GetCellFromChipWell(string chip, string chipWell);
        int GetIdOfChip(string chipId);
        void ResetQueue();
        ProjectDescription GetNextProjectInQueue();
        Dictionary<string, List<MailTaskDescription>> GetQueuedMailTasksByEmail();
        List<string> GetWaitingFilesToBackup();
        Dictionary<string, int> GetWell2CellIdMapping(string plateid);
        void PublishResults(ProjectDescription pd);
        void ReportReadFileResult(string runId, int read, ReadFileResult r);
        bool SecureStartAnalysis(ProjectDescription pd);
        bool SecureStartRunCopy(string runId, int runNo, string runDate);
        void SetBackupStatus(string readFile, string status);
        void SetIlluminaYield(string runId, uint nReads, uint nPFReads, int lane);
        void UpdateAnalysisStatus(string analysisId, string status);
        void UpdateMailTaskStatus(string id, string status);
        void UpdateRunStatus(string runId, string status, int runNo);
    }
}
