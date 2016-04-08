using System;
using System.Collections.Generic;

namespace Linnarsson.C1
{
    public interface IExpressionDB
    {
        Transcriptome GetTranscriptome(string buildVarAnnot);
        IEnumerable<Transcript> IterTranscriptsFromDB(int transcriptomeId);
        Dictionary<string, int> GetRepeatNamesToTranscriptIdsMap(string buildVarAnnot);
        void InsertTranscriptome(Transcriptome t);
        void InsertChromosomePos(int transcriptomeID, string chrId, int startPos, int endPos);
        void InsertWig(int transcriptomeID, string chrID, int chrPos, int cellID, int count);
        void InsertTranscript(Transcript t);
        bool UpdateTranscriptAnnotations(Transcript t);
        void InsertTranscriptAnnotation(TranscriptAnnotation ta);
        void InsertAnalysisSetup(string projectId, string bowtieIndex, string resultFolder, string parameters);
        void InsertExprBlobs(IEnumerable<ExprBlob> exprBlobIterator, bool mols, string aligner);
    }
}
