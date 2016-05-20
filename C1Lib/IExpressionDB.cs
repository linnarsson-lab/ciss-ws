using System;
using System.Collections.Generic;
using Linnarsson.Mathematics;

namespace Linnarsson.C1
{
    /// <summary>
    /// Calls related to expression and wiggle data
    /// </summary>
    public interface IExpressionDB
    {
        Transcriptome GetTranscriptome(string buildVarAnnot);
        IEnumerable<Transcript> IterTranscriptsFromDB(int transcriptomeId);
        Dictionary<string, int> GetRepeatNamesToTranscriptIdsMap(string buildVarAnnot);
        void InsertTranscriptome(Transcriptome t);
        void InsertChromosomePos(int transcriptomeID, string chrId, int startPos, int endPos);
        void InsertChrWiggle(IEnumerator<Pair<int, int>> wiggle, int cellID, int transcriptomeID, string chr, char strand);
        void InsertTranscript(Transcript t);
        bool UpdateTranscriptAnnotations(Transcript t);
        void InsertTranscriptAnnotation(TranscriptAnnotation ta);
        void InsertAnalysisSetup(string projectId, string bowtieIndex, string resultFolder, string parameters);
        int InsertExprBlobs(IEnumerable<ExprBlob> exprBlobIterator, bool mols, string aligner);
    }
}
