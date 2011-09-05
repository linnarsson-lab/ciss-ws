using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
	public class RestrictionEnzymes
	{

		#region Enzymes from REBASE

		// Semi-automatically generated list

		public static RestrictionEnzyme[] All = new RestrictionEnzyme[] 
		{
			new RestrictionEnzyme("AanI", "TTATAA", 3, 3, "PsiI"),
			new RestrictionEnzyme("AarI", "CACCTGC", 4, 8, ""),
			new RestrictionEnzyme("AasI", "GACNNNNNNGTC", 7, 5, "DrdI"),
			new RestrictionEnzyme("AatI", "AGGCCT", 3, 3, "StuI"),
			new RestrictionEnzyme("AatII", "GACGTC", 5, 1, ""),
			new RestrictionEnzyme("AbsI", "CCTCGAGG", 2, 6, ""),
			new RestrictionEnzyme("AccI", "GTMKAC", 2, 4, ""),
			new RestrictionEnzyme("AccII", "CGCG", 2, 2, "FnuDII"),
			new RestrictionEnzyme("AccIII", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("Acc16I", "TGCGCA", 3, 3, "MstI"),
			new RestrictionEnzyme("Acc36I", "ACCTGC", 4, 8, "BspMI"),
			new RestrictionEnzyme("Acc65I", "GGTACC", 1, 5, "KpnI"),
			new RestrictionEnzyme("AccB1I", "GGYRCC", 1, 5, "HgiCI"),
			new RestrictionEnzyme("AccB7I", "CCANNNNNTGG", 7, 4, "PflMI"),
			new RestrictionEnzyme("AccBSI", "CCGCTC", -3, -3, "BsrBI"),
			new RestrictionEnzyme("AciI", "CCGC", -3, -1, ""),
			new RestrictionEnzyme("AclI", "AACGTT", 2, 4, ""),
			new RestrictionEnzyme("AclWI", "GGATC", 4, 5, "BinI"),
			new RestrictionEnzyme("AcoI", "YGGCCR", 1, 5, "CfrI"),
			new RestrictionEnzyme("AcsI", "RAATTY", 1, 5, "ApoI"),
			new RestrictionEnzyme("AcuI", "CTGAAG", 16, 14, "Eco57I"),
			new RestrictionEnzyme("AcvI", "CACGTG", 3, 3, "PmaCI"),
			new RestrictionEnzyme("AcyI", "GRCGYC", 2, 4, ""),
			new RestrictionEnzyme("AdeI", "CACNNNGTG", 6, 3, "DraIII"),
			new RestrictionEnzyme("AfaI", "GTAC", 2, 2, "RsaI"),
			new RestrictionEnzyme("AfeI", "AGCGCT", 3, 3, "Eco47III"),
			new RestrictionEnzyme("AfiI", "CCNNNNNNNGG", 7, 4, "BsiYI"),
			new RestrictionEnzyme("AflII", "CTTAAG", 1, 5, ""),
			new RestrictionEnzyme("AflIII", "ACRYGT", 1, 5, ""),
			new RestrictionEnzyme("AgeI", "ACCGGT", 1, 5, ""),
			new RestrictionEnzyme("AhdI", "GACNNNNNGTC", 6, 5, "Eam1105I"),
			new RestrictionEnzyme("AhlI", "ACTAGT", 1, 5, "SpeI"),
			new RestrictionEnzyme("AjiI", "CACGTC", -3, -3, "BtrI"),
			new RestrictionEnzyme("AjnI", "CCWGG", 0, 5, "EcoRII"),
			new RestrictionEnzyme("AleI", "CACNNNNGTG", 5, 5, "OliI"),
			new RestrictionEnzyme("AluI", "AGCT", 2, 2, ""),
			new RestrictionEnzyme("AluBI", "AGCT", 2, 2, "AluI"),
			new RestrictionEnzyme("AlwI", "GGATC", 4, 5, "BinI"),
			new RestrictionEnzyme("Alw21I", "GWGCWC", 5, 1, "HgiAI"),
			new RestrictionEnzyme("Alw26I", "GTCTC", 1, 5, "BsmAI"),
			new RestrictionEnzyme("Alw44I", "GTGCAC", 1, 5, "ApaLI"),
			new RestrictionEnzyme("AlwNI", "CAGNNNCTG", 6, 3, ""),
			new RestrictionEnzyme("Ama87I", "CYCGRG", 1, 5, "AvaI"),
			new RestrictionEnzyme("Aor13HI", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("Aor51HI", "AGCGCT", 3, 3, "Eco47III"),
			new RestrictionEnzyme("ApaI", "GGGCCC", 5, 1, ""),
			new RestrictionEnzyme("ApaLI", "GTGCAC", 1, 5, ""),
			new RestrictionEnzyme("ApeKI", "GCWGC", 1, 4, "TseI"),
			new RestrictionEnzyme("ApoI", "RAATTY", 1, 5, ""),
			new RestrictionEnzyme("AscI", "GGCGCGCC", 2, 6, ""),
			new RestrictionEnzyme("AseI", "ATTAAT", 2, 4, "VspI"),
			new RestrictionEnzyme("AsiGI", "ACCGGT", 1, 5, "AgeI"),
			new RestrictionEnzyme("AsiSI", "GCGATCGC", 5, 3, "SgfI"),
			new RestrictionEnzyme("AspI", "GACNNNGTC", 4, 5, "Tth111I"),
			new RestrictionEnzyme("Asp700I", "GAANNNNTTC", 5, 5, "XmnI"),
			new RestrictionEnzyme("Asp718I", "GGTACC", 1, 5, "KpnI"),
			new RestrictionEnzyme("AspA2I", "CCTAGG", 1, 5, "AvrII"),
			new RestrictionEnzyme("AspEI", "GACNNNNNGTC", 6, 5, "Eam1105I"),
			new RestrictionEnzyme("AspLEI", "GCGC", 3, 1, "HhaI"),
			new RestrictionEnzyme("AspS9I", "GGNCC", 1, 4, "AsuI"),
			new RestrictionEnzyme("AssI", "AGTACT", 3, 3, "ScaI"),
			new RestrictionEnzyme("AsuC2I", "CCSGG", 2, 3, "CauII"),
			new RestrictionEnzyme("AsuHPI", "GGTGA", 8, 7, "HphI"),
			new RestrictionEnzyme("AsuNHI", "GCTAGC", 1, 5, "NheI"),
			new RestrictionEnzyme("AvaI", "CYCGRG", 1, 5, ""),
			new RestrictionEnzyme("AvaII", "GGWCC", 1, 4, ""),
			new RestrictionEnzyme("AviII", "TGCGCA", 3, 3, "MstI"),
			new RestrictionEnzyme("AvrII", "CCTAGG", 1, 5, ""),
			new RestrictionEnzyme("AxyI", "CCTNAGG", 2, 5, "SauI"),
			new RestrictionEnzyme("BaeGI", "GKGCMC", 5, 1, "BseSI"),
			new RestrictionEnzyme("BalI", "TGGCCA", 3, 3, ""),
			new RestrictionEnzyme("BamHI", "GGATCC", 1, 5, ""),
			new RestrictionEnzyme("BanI", "GGYRCC", 1, 5, "HgiCI"),
			new RestrictionEnzyme("BanII", "GRGCYC", 5, 1, "HgiJII"),
			new RestrictionEnzyme("BanIII", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BasI", "CCANNNNNTGG", 7, 4, "PflMI"),
			new RestrictionEnzyme("BauI", "CACGAG", -5, -1, "BsiI"),
			new RestrictionEnzyme("BbeI", "GGCGCC", 5, 1, "NarI"),
			new RestrictionEnzyme("BbrPI", "CACGTG", 3, 3, "PmaCI"),
			new RestrictionEnzyme("BbsI", "GAAGAC", 2, 6, "BbvII"),
			new RestrictionEnzyme("BbuI", "GCATGC", 5, 1, "SphI"),
			new RestrictionEnzyme("BbvI", "GCAGC", 8, 12, ""),
			new RestrictionEnzyme("Bbv12I", "GWGCWC", 5, 1, "HgiAI"),
			new RestrictionEnzyme("BbvCI", "CCTCAGC", -5, -2, ""),
			new RestrictionEnzyme("BccI", "CCATC", 4, 5, ""),
			new RestrictionEnzyme("BceAI", "ACGGC", 12, 14, "BcefI"),
			new RestrictionEnzyme("BciVI", "GTATCC", 6, 5, ""),
			new RestrictionEnzyme("BclI", "TGATCA", 1, 5, ""),
			new RestrictionEnzyme("BcnI", "CCSGG", 2, 3, "CauII"),
			new RestrictionEnzyme("BcuI", "ACTAGT", 1, 5, "SpeI"),
			new RestrictionEnzyme("BfaI", "CTAG", 1, 3, "MaeI"),
			new RestrictionEnzyme("BfiI", "ACTGGG", 5, 4, ""),
			new RestrictionEnzyme("BfmI", "CTRYAG", 1, 5, "SfeI"),
			new RestrictionEnzyme("BfrI", "CTTAAG", 1, 5, "AflII"),
			new RestrictionEnzyme("BfuI", "GTATCC", 6, 5, "BciVI"),
			new RestrictionEnzyme("BfuAI", "ACCTGC", 4, 8, "BspMI"),
			new RestrictionEnzyme("BfuCI", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("BglI", "GCCNNNNNGGC", 7, 4, ""),
			new RestrictionEnzyme("BglII", "AGATCT", 1, 5, ""),
			new RestrictionEnzyme("BisI", "GCNGC", 2, 3, ""),
			new RestrictionEnzyme("BlnI", "CCTAGG", 1, 5, "AvrII"),
			new RestrictionEnzyme("BlpI", "GCTNAGC", 2, 5, "EspI"),
			new RestrictionEnzyme("BlsI", "GCNGC", 3, 2, "BisI"),
			new RestrictionEnzyme("BmcAI", "AGTACT", 3, 3, "ScaI"),
			new RestrictionEnzyme("Bme18I", "GGWCC", 1, 4, "AvaII"),
			new RestrictionEnzyme("Bme1390I", "CCNGG", 2, 3, "ScrFI"),
			new RestrictionEnzyme("BmeRI", "GACNNNNNGTC", 6, 5, "Eam1105I"),
			new RestrictionEnzyme("BmeT110I", "CYCGRG", 2, 4, "AvaI"),
			new RestrictionEnzyme("BmgBI", "CACGTC", -3, -3, "BtrI"),
			new RestrictionEnzyme("BmgT120I", "GGNCC", 2, 3, "AsuI"),
			new RestrictionEnzyme("BmiI", "GGNNCC", 3, 3, "NlaIV"),
			new RestrictionEnzyme("BmrI", "ACTGGG", 5, 4, "BfiI"),
			new RestrictionEnzyme("BmrFI", "CCNGG", 2, 3, "ScrFI"),
			new RestrictionEnzyme("BmtI", "GCTAGC", 5, 1, "NheI"),
			new RestrictionEnzyme("BmuI", "ACTGGG", 5, 4, "BfiI"),
			new RestrictionEnzyme("BoxI", "GACNNNNGTC", 5, 5, "PshAI"),
			new RestrictionEnzyme("BpiI", "GAAGAC", 2, 6, "BbvII"),
			new RestrictionEnzyme("BpmI", "CTGGAG", 16, 14, "GsuI"),
			new RestrictionEnzyme("Bpu10I", "CCTNAGC", -5, -2, ""),
			new RestrictionEnzyme("Bpu14I", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("Bpu1102I", "GCTNAGC", 2, 5, "EspI"),
			new RestrictionEnzyme("BpuAI", "GAAGAC", 2, 6, "BbvII"),
			new RestrictionEnzyme("BpuEI", "CTTGAG", 16, 14, "Bce83I"),
			new RestrictionEnzyme("BpuMI", "CCSGG", 2, 3, "CauII"),
			new RestrictionEnzyme("BpvUI", "CGATCG", 4, 2, "PvuI"),
			new RestrictionEnzyme("BsaI", "GGTCTC", 1, 5, "Eco31I"),
			new RestrictionEnzyme("Bsa29I", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BsaAI", "YACGTR", 3, 3, ""),
			new RestrictionEnzyme("BsaBI", "GATNNNNATC", 5, 5, ""),
			new RestrictionEnzyme("BsaHI", "GRCGYC", 2, 4, "AcyI"),
			new RestrictionEnzyme("BsaJI", "CCNNGG", 1, 5, "SecI"),
			new RestrictionEnzyme("BsaMI", "GAATGC", 1, -1, "BsmI"),
			new RestrictionEnzyme("BsaWI", "WCCGGW", 1, 5, "BetI"),
			new RestrictionEnzyme("Bsc4I", "CCNNNNNNNGG", 7, 4, "BsiYI"),
			new RestrictionEnzyme("Bse1I", "ACTGG", 1, -1, "BsrI"),
			new RestrictionEnzyme("Bse8I", "GATNNNNATC", 5, 5, "BsaBI"),
			new RestrictionEnzyme("Bse21I", "CCTNAGG", 2, 5, "SauI"),
			new RestrictionEnzyme("Bse118I", "RCCGGY", 1, 5, "Cfr10I"),
			new RestrictionEnzyme("BseAI", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("BseBI", "CCWGG", 2, 3, "EcoRII"),
			new RestrictionEnzyme("BseCI", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BseDI", "CCNNGG", 1, 5, "SecI"),
			new RestrictionEnzyme("Bse3DI", "GCAATG", 2, 0, "BsrDI"),
			new RestrictionEnzyme("BseGI", "GGATG", 2, 0, "FokI"),
			new RestrictionEnzyme("BseJI", "GATNNNNATC", 5, 5, "BsaBI"),
			new RestrictionEnzyme("BseLI", "CCNNNNNNNGG", 7, 4, "BsiYI"),
			new RestrictionEnzyme("BseMI", "GCAATG", 2, 0, "BsrDI"),
			new RestrictionEnzyme("BseMII", "CTCAG", 10, 8, ""),
			new RestrictionEnzyme("BseNI", "ACTGG", 1, -1, "BsrI"),
			new RestrictionEnzyme("BsePI", "GCGCGC", 1, 5, ""),
			new RestrictionEnzyme("BseRI", "GAGGAG", 10, 8, ""),
			new RestrictionEnzyme("BseSI", "GKGCMC", 5, 1, ""),
			new RestrictionEnzyme("BseXI", "GCAGC", 8, 12, "BbvI"),
			new RestrictionEnzyme("BseX3I", "CGGCCG", 1, 5, "XmaIII"),
			new RestrictionEnzyme("BseYI", "CCCAGC", -5, -1, ""),
			new RestrictionEnzyme("BsgI", "GTGCAG", 16, 14, ""),
			new RestrictionEnzyme("Bsh1236I", "CGCG", 2, 2, "FnuDII"),
			new RestrictionEnzyme("Bsh1285I", "CGRYCG", 4, 2, "McrI"),
			new RestrictionEnzyme("BshFI", "GGCC", 2, 2, "HaeIII"),
			new RestrictionEnzyme("BshNI", "GGYRCC", 1, 5, "HgiCI"),
			new RestrictionEnzyme("BshTI", "ACCGGT", 1, 5, "AgeI"),
			new RestrictionEnzyme("BshVI", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BsiEI", "CGRYCG", 4, 2, "McrI"),
			new RestrictionEnzyme("BsiHKAI", "GWGCWC", 5, 1, "HgiAI"),
			new RestrictionEnzyme("BsiHKCI", "CYCGRG", 1, 5, "AvaI"),
			new RestrictionEnzyme("BsiSI", "CCGG", 1, 3, "HpaII"),
			new RestrictionEnzyme("BsiWI", "CGTACG", 1, 5, "SplI"),
			new RestrictionEnzyme("BslI", "CCNNNNNNNGG", 7, 4, "BsiYI"),
			new RestrictionEnzyme("BslFI", "GGGAC", 10, 14, "FinI"),
			new RestrictionEnzyme("BsmI", "GAATGC", 1, -1, ""),
			new RestrictionEnzyme("BsmAI", "GTCTC", 1, 5, ""),
			new RestrictionEnzyme("BsmBI", "CGTCTC", 1, 5, "Esp3I"),
			new RestrictionEnzyme("BsmFI", "GGGAC", 10, 14, "FinI"),
			new RestrictionEnzyme("BsnI", "GGCC", 2, 2, "HaeIII"),
			new RestrictionEnzyme("Bso31I", "GGTCTC", 1, 5, "Eco31I"),
			new RestrictionEnzyme("BsoBI", "CYCGRG", 1, 5, "AvaI"),
			new RestrictionEnzyme("Bsp13I", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("Bsp19I", "CCATGG", 1, 5, "NcoI"),
			new RestrictionEnzyme("Bsp68I", "TCGCGA", 3, 3, "NruI"),
			new RestrictionEnzyme("Bsp119I", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("Bsp120I", "GGGCCC", 1, 5, "ApaI"),
			new RestrictionEnzyme("Bsp143I", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("Bsp1286I", "GDGCHC", 5, 1, "SduI"),
			new RestrictionEnzyme("Bsp1407I", "TGTACA", 1, 5, ""),
			new RestrictionEnzyme("Bsp1720I", "GCTNAGC", 2, 5, "EspI"),
			new RestrictionEnzyme("BspACI", "CCGC", -3, -1, "AciI"),
			new RestrictionEnzyme("BspANI", "GGCC", 2, 2, "HaeIII"),
			new RestrictionEnzyme("BspCNI", "CTCAG", 9, 7, "BseMII"),
			new RestrictionEnzyme("BspDI", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BspEI", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("BspFNI", "CGCG", 2, 2, "FnuDII"),
			new RestrictionEnzyme("BspHI", "TCATGA", 1, 5, ""),
			new RestrictionEnzyme("BspLI", "GGNNCC", 3, 3, "NlaIV"),
			new RestrictionEnzyme("BspMI", "ACCTGC", 4, 8, ""),
			new RestrictionEnzyme("BspMAI", "CTGCAG", 5, 1, "PstI"),
			new RestrictionEnzyme("BspOI", "GCTAGC", 5, 1, "NheI"),
			new RestrictionEnzyme("BspPI", "GGATC", 4, 5, "BinI"),
			new RestrictionEnzyme("BspQI", "GCTCTTC", 1, 4, "SapI"),
			new RestrictionEnzyme("BspTI", "CTTAAG", 1, 5, "AflII"),
			new RestrictionEnzyme("BspT104I", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("BspT107I", "GGYRCC", 1, 5, "HgiCI"),
			new RestrictionEnzyme("BspTNI", "GGTCTC", 1, 5, "Eco31I"),
			new RestrictionEnzyme("BspXI", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BsrI", "ACTGG", 1, -1, ""),
			new RestrictionEnzyme("BsrBI", "CCGCTC", -3, -3, ""),
			new RestrictionEnzyme("BsrDI", "GCAATG", 2, 0, ""),
			new RestrictionEnzyme("BsrFI", "RCCGGY", 1, 5, "Cfr10I"),
			new RestrictionEnzyme("BsrGI", "TGTACA", 1, 5, "Bsp1407I"),
			new RestrictionEnzyme("BsrSI", "ACTGG", 1, -1, "BsrI"),
			new RestrictionEnzyme("BssAI", "RCCGGY", 1, 5, "Cfr10I"),
			new RestrictionEnzyme("BssECI", "CCNNGG", 1, 5, "SecI"),
			new RestrictionEnzyme("BssHII", "GCGCGC", 1, 5, "BsePI"),
			new RestrictionEnzyme("BssKI", "CCNGG", 0, 5, "ScrFI"),
			new RestrictionEnzyme("BssMI", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("BssNI", "GRCGYC", 2, 4, "AcyI"),
			new RestrictionEnzyme("BssNAI", "GTATAC", 3, 3, "SnaI"),
			new RestrictionEnzyme("BssSI", "CACGAG", -5, -1, "BsiI"),
			new RestrictionEnzyme("BssT1I", "CCWWGG", 1, 5, "StyI"),
			new RestrictionEnzyme("Bst6I", "CTCTTC", 1, 4, "Ksp632I"),
			new RestrictionEnzyme("Bst98I", "CTTAAG", 1, 5, "AflII"),
			new RestrictionEnzyme("Bst1107I", "GTATAC", 3, 3, "SnaI"),
			new RestrictionEnzyme("BstACI", "GRCGYC", 2, 4, "AcyI"),
			new RestrictionEnzyme("BstAFI", "CTTAAG", 1, 5, "AflII"),
			new RestrictionEnzyme("BstAPI", "GCANNNNNTGC", 7, 4, "ApaBI"),
			new RestrictionEnzyme("BstAUI", "TGTACA", 1, 5, "Bsp1407I"),
			new RestrictionEnzyme("BstBI", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("Bst2BI", "CACGAG", -5, -1, "BsiI"),
			new RestrictionEnzyme("BstBAI", "YACGTR", 3, 3, "BsaAI"),
			new RestrictionEnzyme("Bst4CI", "ACNGT", 3, 2, "Tsp4CI"),
			new RestrictionEnzyme("BstC8I", "GCNNGC", 3, 3, "Cac8I"),
			new RestrictionEnzyme("BstDEI", "CTNAG", 1, 4, "DdeI"),
			new RestrictionEnzyme("BstDSI", "CCRYGG", 1, 5, "DsaI"),
			new RestrictionEnzyme("BstEII", "GGTNACC", 1, 6, ""),
			new RestrictionEnzyme("BstENI", "CCTNNNNNAGG", 5, 6, "EcoNI"),
			new RestrictionEnzyme("BstF5I", "GGATG", 2, 0, "FokI"),
			new RestrictionEnzyme("BstFNI", "CGCG", 2, 2, "FnuDII"),
			new RestrictionEnzyme("BstH2I", "RGCGCY", 5, 1, "HaeII"),
			new RestrictionEnzyme("BstHHI", "GCGC", 3, 1, "HhaI"),
			new RestrictionEnzyme("BstKTI", "GATC", 3, 1, "MboI"),
			new RestrictionEnzyme("BstMAI", "GTCTC", 1, 5, "BsmAI"),
			new RestrictionEnzyme("BstMBI", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("BstMCI", "CGRYCG", 4, 2, "McrI"),
			new RestrictionEnzyme("BstMWI", "GCNNNNNNNGC", 7, 4, "MwoI"),
			new RestrictionEnzyme("BstNI", "CCWGG", 2, 3, "EcoRII"),
			new RestrictionEnzyme("BstNSI", "RCATGY", 5, 1, "NspI"),
			new RestrictionEnzyme("BstOI", "CCWGG", 2, 3, "EcoRII"),
			new RestrictionEnzyme("BstPI", "GGTNACC", 1, 6, "BstEII"),
			new RestrictionEnzyme("BstPAI", "GACNNNNGTC", 5, 5, "PshAI"),
			new RestrictionEnzyme("BstSCI", "CCNGG", 0, 5, "ScrFI"),
			new RestrictionEnzyme("BstSFI", "CTRYAG", 1, 5, "SfeI"),
			new RestrictionEnzyme("BstSLI", "GKGCMC", 5, 1, "BseSI"),
			new RestrictionEnzyme("BstSNI", "TACGTA", 3, 3, "SnaBI"),
			new RestrictionEnzyme("BstUI", "CGCG", 2, 2, "FnuDII"),
			new RestrictionEnzyme("Bst2UI", "CCWGG", 2, 3, "EcoRII"),
			new RestrictionEnzyme("BstV1I", "GCAGC", 8, 12, "BbvI"),
			new RestrictionEnzyme("BstV2I", "GAAGAC", 2, 6, "BbvII"),
			new RestrictionEnzyme("BstXI", "CCANNNNNNTGG", 8, 4, ""),
			new RestrictionEnzyme("BstX2I", "RGATCY", 1, 5, "XhoII"),
			new RestrictionEnzyme("BstYI", "RGATCY", 1, 5, "XhoII"),
			new RestrictionEnzyme("BstZI", "CGGCCG", 1, 5, "XmaIII"),
			new RestrictionEnzyme("BstZ17I", "GTATAC", 3, 3, "SnaI"),
			new RestrictionEnzyme("Bsu15I", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("Bsu36I", "CCTNAGG", 2, 5, "SauI"),
			new RestrictionEnzyme("BsuRI", "GGCC", 2, 2, "HaeIII"),
			new RestrictionEnzyme("BsuTUI", "ATCGAT", 2, 4, "ClaI"),
			new RestrictionEnzyme("BtgI", "CCRYGG", 1, 5, "DsaI"),
			new RestrictionEnzyme("BtgZI", "GCGATG", 10, 14, ""),
			new RestrictionEnzyme("BtrI", "CACGTC", -3, -3, ""),
			new RestrictionEnzyme("BtsI", "GCAGTG", 2, 0, ""),
			new RestrictionEnzyme("BtsCI", "GGATG", 2, 0, "FokI"),
			new RestrictionEnzyme("BtuMI", "TCGCGA", 3, 3, "NruI"),
			new RestrictionEnzyme("BveI", "ACCTGC", 4, 8, "BspMI"),
			new RestrictionEnzyme("Cac8I", "GCNNGC", 3, 3, ""),
			new RestrictionEnzyme("CaiI", "CAGNNNCTG", 6, 3, "AlwNI"),
			new RestrictionEnzyme("CciI", "TCATGA", 1, 5, "BspHI"),
			new RestrictionEnzyme("CciNI", "GCGGCCGC", 2, 6, "NotI"),
			new RestrictionEnzyme("CelII", "GCTNAGC", 2, 5, "EspI"),
			new RestrictionEnzyme("CfoI", "GCGC", 3, 1, "HhaI"),
			new RestrictionEnzyme("CfrI", "YGGCCR", 1, 5, ""),
			new RestrictionEnzyme("Cfr9I", "CCCGGG", 1, 5, "SmaI"),
			new RestrictionEnzyme("Cfr10I", "RCCGGY", 1, 5, ""),
			new RestrictionEnzyme("Cfr13I", "GGNCC", 1, 4, "AsuI"),
			new RestrictionEnzyme("Cfr42I", "CCGCGG", 4, 2, "SacII"),
			new RestrictionEnzyme("ClaI", "ATCGAT", 2, 4, ""),
			new RestrictionEnzyme("CpoI", "CGGWCCG", 2, 5, "RsrII"),
			new RestrictionEnzyme("CseI", "GACGC", 5, 10, "HgaI"),
			new RestrictionEnzyme("CspI", "CGGWCCG", 2, 5, "RsrII"),
			new RestrictionEnzyme("Csp6I", "GTAC", 1, 3, "RsaI"),
			new RestrictionEnzyme("Csp45I", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("CspAI", "ACCGGT", 1, 5, "AgeI"),
			new RestrictionEnzyme("CviAII", "CATG", 1, 3, "NlaIII"),
			new RestrictionEnzyme("CviJI", "RGCY", 2, 2, ""),
			new RestrictionEnzyme("CviKI1", "RGCY", 2, 2, "CviJI"),
			new RestrictionEnzyme("CviQI", "GTAC", 1, 3, "RsaI"),
			new RestrictionEnzyme("DdeI", "CTNAG", 1, 4, ""),
			new RestrictionEnzyme("DinI", "GGCGCC", 3, 3, "NarI"),
			new RestrictionEnzyme("DpnI", "GATC", 2, 2, ""),
			new RestrictionEnzyme("DpnII", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("DraI", "TTTAAA", 3, 3, "AhaIII"),
			new RestrictionEnzyme("DraII", "RGGNCCY", 2, 5, ""),
			new RestrictionEnzyme("DraIII", "CACNNNGTG", 6, 3, ""),
			new RestrictionEnzyme("DrdI", "GACNNNNNNGTC", 7, 5, ""),
			new RestrictionEnzyme("DriI", "GACNNNNNGTC", 6, 5, "Eam1105I"),
			new RestrictionEnzyme("DseDI", "GACNNNNNNGTC", 7, 5, "DrdI"),
			new RestrictionEnzyme("EaeI", "YGGCCR", 1, 5, "CfrI"),
			new RestrictionEnzyme("EagI", "CGGCCG", 1, 5, "XmaIII"),
			new RestrictionEnzyme("Eam1104I", "CTCTTC", 1, 4, "Ksp632I"),
			new RestrictionEnzyme("Eam1105I", "GACNNNNNGTC", 6, 5, ""),
			new RestrictionEnzyme("EarI", "CTCTTC", 1, 4, "Ksp632I"),
			new RestrictionEnzyme("EciI", "GGCGGA", 11, 9, ""),
			new RestrictionEnzyme("Ecl136II", "GAGCTC", 3, 3, "SacI"),
			new RestrictionEnzyme("EclHKI", "GACNNNNNGTC", 6, 5, "Eam1105I"),
			new RestrictionEnzyme("EclXI", "CGGCCG", 1, 5, "XmaIII"),
			new RestrictionEnzyme("Eco24I", "GRGCYC", 5, 1, "HgiJII"),
			new RestrictionEnzyme("Eco31I", "GGTCTC", 1, 5, ""),
			new RestrictionEnzyme("Eco32I", "GATATC", 3, 3, "EcoRV"),
			new RestrictionEnzyme("Eco47I", "GGWCC", 1, 4, "AvaII"),
			new RestrictionEnzyme("Eco47III", "AGCGCT", 3, 3, ""),
			new RestrictionEnzyme("Eco52I", "CGGCCG", 1, 5, "XmaIII"),
			new RestrictionEnzyme("Eco57I", "CTGAAG", 16, 14, ""),
			new RestrictionEnzyme("Eco72I", "CACGTG", 3, 3, "PmaCI"),
			new RestrictionEnzyme("Eco81I", "CCTNAGG", 2, 5, "SauI"),
			new RestrictionEnzyme("Eco88I", "CYCGRG", 1, 5, "AvaI"),
			new RestrictionEnzyme("Eco91I", "GGTNACC", 1, 6, "BstEII"),
			new RestrictionEnzyme("Eco105I", "TACGTA", 3, 3, "SnaBI"),
			new RestrictionEnzyme("Eco130I", "CCWWGG", 1, 5, "StyI"),
			new RestrictionEnzyme("Eco147I", "AGGCCT", 3, 3, "StuI"),
			new RestrictionEnzyme("EcoICRI", "GAGCTC", 3, 3, "SacI"),
			new RestrictionEnzyme("Eco57MI", "CTGRAG", 16, 14, ""),
			new RestrictionEnzyme("EcoNI", "CCTNNNNNAGG", 5, 6, ""),
			new RestrictionEnzyme("EcoO65I", "GGTNACC", 1, 6, "BstEII"),
			new RestrictionEnzyme("EcoO109I", "RGGNCCY", 2, 5, "DraII"),
			new RestrictionEnzyme("EcoRI", "GAATTC", 1, 5, ""),
			new RestrictionEnzyme("EcoRII", "CCWGG", 0, 5, ""),
			new RestrictionEnzyme("EcoRV", "GATATC", 3, 3, ""),
			new RestrictionEnzyme("EcoT14I", "CCWWGG", 1, 5, "StyI"),
			new RestrictionEnzyme("EcoT22I", "ATGCAT", 5, 1, "AvaIII"),
			new RestrictionEnzyme("EcoT38I", "GRGCYC", 5, 1, "HgiJII"),
			new RestrictionEnzyme("EgeI", "GGCGCC", 3, 3, "NarI"),
			new RestrictionEnzyme("EheI", "GGCGCC", 3, 3, "NarI"),
			new RestrictionEnzyme("ErhI", "CCWWGG", 1, 5, "StyI"),
			new RestrictionEnzyme("Esp3I", "CGTCTC", 1, 5, ""),
			new RestrictionEnzyme("FaeI", "CATG", 4, 0, "NlaIII"),
			new RestrictionEnzyme("FaqI", "GGGAC", 10, 14, "FinI"),
			new RestrictionEnzyme("FatI", "CATG", 0, 4, "NlaIII"),
			new RestrictionEnzyme("FauI", "CCCGC", 4, 6, ""),
			new RestrictionEnzyme("FauNDI", "CATATG", 2, 4, "NdeI"),
			new RestrictionEnzyme("FbaI", "TGATCA", 1, 5, "BclI"),
			new RestrictionEnzyme("FblI", "GTMKAC", 2, 4, "AccI"),
			new RestrictionEnzyme("Fnu4HI", "GCNGC", 2, 3, ""),
			new RestrictionEnzyme("FokI", "GGATG", 9, 13, ""),
			new RestrictionEnzyme("FriOI", "GRGCYC", 5, 1, "HgiJII"),
			new RestrictionEnzyme("FseI", "GGCCGGCC", 6, 2, ""),
			new RestrictionEnzyme("FspI", "TGCGCA", 3, 3, "MstI"),
			new RestrictionEnzyme("FspAI", "RTGCGCAY", 4, 4, ""),
			new RestrictionEnzyme("FspBI", "CTAG", 1, 3, "MaeI"),
			new RestrictionEnzyme("Fsp4HI", "GCNGC", 2, 3, "Fnu4HI"),
			new RestrictionEnzyme("GlaI", "GCGC", 2, 2, ""),
			new RestrictionEnzyme("GluI", "GCNGC", 2, 3, "BisI"),
			new RestrictionEnzyme("GsaI", "CCCAGC", -1, -5, "BseYI"),
			new RestrictionEnzyme("GsuI", "CTGGAG", 16, 14, ""),
			new RestrictionEnzyme("HaeII", "RGCGCY", 5, 1, ""),
			new RestrictionEnzyme("HaeIII", "GGCC", 2, 2, ""),
			new RestrictionEnzyme("HapII", "CCGG", 1, 3, "HpaII"),
			new RestrictionEnzyme("HgaI", "GACGC", 5, 10, ""),
			new RestrictionEnzyme("HhaI", "GCGC", 3, 1, ""),
			new RestrictionEnzyme("Hin1I", "GRCGYC", 2, 4, "AcyI"),
			new RestrictionEnzyme("Hin1II", "CATG", 4, 0, "NlaIII"),
			new RestrictionEnzyme("Hin6I", "GCGC", 1, 3, "HhaI"),
			new RestrictionEnzyme("HinP1I", "GCGC", 1, 3, "HhaI"),
			new RestrictionEnzyme("HincII", "GTYRAC", 3, 3, "HindII"),
			new RestrictionEnzyme("HindII", "GTYRAC", 3, 3, ""),
			new RestrictionEnzyme("HindIII", "AAGCTT", 1, 5, ""),
			new RestrictionEnzyme("HinfI", "GANTC", 1, 4, ""),
			new RestrictionEnzyme("HpaI", "GTTAAC", 3, 3, ""),
			new RestrictionEnzyme("HpaII", "CCGG", 1, 3, ""),
			new RestrictionEnzyme("HphI", "GGTGA", 8, 7, ""),
			new RestrictionEnzyme("Hpy8I", "GTNNAC", 3, 3, "MjaIV"),
			new RestrictionEnzyme("Hpy99I", "CGWCG", 5, 0, ""),
			new RestrictionEnzyme("Hpy166II", "GTNNAC", 3, 3, "MjaIV"),
			new RestrictionEnzyme("Hpy188I", "TCNGA", 3, 2, ""),
			new RestrictionEnzyme("Hpy188III", "TCNNGA", 2, 4, "Hpy178III"),
			new RestrictionEnzyme("HpyAV", "CCTTC", 6, 5, "Hin4II"),
			new RestrictionEnzyme("HpyCH4III", "ACNGT", 3, 2, "Tsp4CI"),
			new RestrictionEnzyme("HpyCH4IV", "ACGT", 1, 3, "MaeII"),
			new RestrictionEnzyme("HpyCH4V", "TGCA", 2, 2, "CviRI"),
			new RestrictionEnzyme("HpyF3I", "CTNAG", 1, 4, "DdeI"),
			new RestrictionEnzyme("HpyF10VI", "GCNNNNNNNGC", 7, 4, "MwoI"),
			new RestrictionEnzyme("Hsp92I", "GRCGYC", 2, 4, "AcyI"),
			new RestrictionEnzyme("Hsp92II", "CATG", 4, 0, "NlaIII"),
			new RestrictionEnzyme("HspAI", "GCGC", 1, 3, "HhaI"),
			new RestrictionEnzyme("ItaI", "GCNGC", 2, 3, "Fnu4HI"),
			new RestrictionEnzyme("KasI", "GGCGCC", 1, 5, "NarI"),
			new RestrictionEnzyme("KpnI", "GGTACC", 5, 1, ""),
			new RestrictionEnzyme("Kpn2I", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("KspI", "CCGCGG", 4, 2, "SacII"),
			new RestrictionEnzyme("Ksp22I", "TGATCA", 1, 5, "BclI"),
			new RestrictionEnzyme("KspAI", "GTTAAC", 3, 3, "HpaI"),
			new RestrictionEnzyme("Kzo9I", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("LguI", "GCTCTTC", 1, 4, "SapI"),
			new RestrictionEnzyme("Lsp1109I", "GCAGC", 8, 12, "BbvI"),
			new RestrictionEnzyme("LweI", "GCATC", 5, 9, "SfaNI"),
			new RestrictionEnzyme("MabI", "ACCWGGT", 1, 6, "SexAI"),
			new RestrictionEnzyme("MaeI", "CTAG", 1, 3, ""),
			new RestrictionEnzyme("MaeII", "ACGT", 1, 3, ""),
			new RestrictionEnzyme("MaeIII", "GTNAC", 0, 5, ""),
			new RestrictionEnzyme("MalI", "GATC", 2, 2, "DpnI"),
			new RestrictionEnzyme("MauBI", "CGCGCGCG", 2, 6, ""),
			new RestrictionEnzyme("MbiI", "CCGCTC", -3, -3, "BsrBI"),
			new RestrictionEnzyme("MboI", "GATC", 0, 4, ""),
			new RestrictionEnzyme("MboII", "GAAGA", 8, 7, ""),
			new RestrictionEnzyme("MfeI", "CAATTG", 1, 5, ""),
			new RestrictionEnzyme("MflI", "RGATCY", 1, 5, "XhoII"),
			new RestrictionEnzyme("MhlI", "GDGCHC", 5, 1, "SduI"),
			new RestrictionEnzyme("MlsI", "TGGCCA", 3, 3, "BalI"),
			new RestrictionEnzyme("MluI", "ACGCGT", 1, 5, ""),
			new RestrictionEnzyme("MluNI", "TGGCCA", 3, 3, "BalI"),
			new RestrictionEnzyme("MlyI", "GAGTC", 5, 5, "PleI"),
			new RestrictionEnzyme("Mly113I", "GGCGCC", 2, 4, "NarI"),
			new RestrictionEnzyme("MmeI", "TCCRAC", 20, 18, ""),
			new RestrictionEnzyme("MnlI", "CCTC", 7, 6, ""),
			new RestrictionEnzyme("Mph1103I", "ATGCAT", 5, 1, "AvaIII"),
			new RestrictionEnzyme("MreI", "CGCCGGCG", 2, 6, "Sse232I"),
			new RestrictionEnzyme("MroI", "TCCGGA", 1, 5, "BspMII"),
			new RestrictionEnzyme("MroNI", "GCCGGC", 1, 5, "NaeI"),
			new RestrictionEnzyme("MroXI", "GAANNNNTTC", 5, 5, "XmnI"),
			new RestrictionEnzyme("MscI", "TGGCCA", 3, 3, "BalI"),
			new RestrictionEnzyme("MseI", "TTAA", 1, 3, ""),
			new RestrictionEnzyme("MslI", "CAYNNNNRTG", 5, 5, ""),
			new RestrictionEnzyme("MspI", "CCGG", 1, 3, "HpaII"),
			new RestrictionEnzyme("Msp20I", "TGGCCA", 3, 3, "BalI"),
			new RestrictionEnzyme("MspA1I", "CMGCKG", 3, 3, "NspBII"),
			new RestrictionEnzyme("MspCI", "CTTAAG", 1, 5, "AflII"),
			new RestrictionEnzyme("MspR9I", "CCNGG", 2, 3, "ScrFI"),
			new RestrictionEnzyme("MssI", "GTTTAAAC", 4, 4, "PmeI"),
			new RestrictionEnzyme("MunI", "CAATTG", 1, 5, "MfeI"),
			new RestrictionEnzyme("MvaI", "CCWGG", 2, 3, "EcoRII"),
			new RestrictionEnzyme("Mva1269I", "GAATGC", 1, -1, "BsmI"),
			new RestrictionEnzyme("MvnI", "CGCG", 2, 2, "FnuDII"),
			new RestrictionEnzyme("MvrI", "CGATCG", 4, 2, "PvuI"),
			new RestrictionEnzyme("MwoI", "GCNNNNNNNGC", 7, 4, ""),
			new RestrictionEnzyme("NaeI", "GCCGGC", 3, 3, ""),
			new RestrictionEnzyme("NarI", "GGCGCC", 2, 4, ""),
			new RestrictionEnzyme("NciI", "CCSGG", 2, 3, "CauII"),
			new RestrictionEnzyme("NcoI", "CCATGG", 1, 5, ""),
			new RestrictionEnzyme("NdeI", "CATATG", 2, 4, ""),
			new RestrictionEnzyme("NdeII", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("NgoMIV", "GCCGGC", 1, 5, "NaeI"),
			new RestrictionEnzyme("NheI", "GCTAGC", 1, 5, ""),
			new RestrictionEnzyme("NlaIII", "CATG", 4, 0, ""),
			new RestrictionEnzyme("NlaIV", "GGNNCC", 3, 3, ""),
			new RestrictionEnzyme("NmeAIII", "GCCGAG", 21, 19, ""),
			new RestrictionEnzyme("NmuCI", "GTSAC", 0, 5, "Tsp45I"),
			new RestrictionEnzyme("NotI", "GCGGCCGC", 2, 6, ""),
			new RestrictionEnzyme("NruI", "TCGCGA", 3, 3, ""),
			new RestrictionEnzyme("NsbI", "TGCGCA", 3, 3, "MstI"),
			new RestrictionEnzyme("NsiI", "ATGCAT", 5, 1, "AvaIII"),
			new RestrictionEnzyme("NspI", "RCATGY", 5, 1, ""),
			new RestrictionEnzyme("NspV", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("OliI", "CACNNNNGTG", 5, 5, ""),
			new RestrictionEnzyme("PacI", "TTAATTAA", 5, 3, ""),
			new RestrictionEnzyme("PaeI", "GCATGC", 5, 1, "SphI"),
			new RestrictionEnzyme("PaeR7I", "CTCGAG", 1, 5, "XhoI"),
			new RestrictionEnzyme("PagI", "TCATGA", 1, 5, "BspHI"),
			new RestrictionEnzyme("PalAI", "GGCGCGCC", 2, 6, "AscI"),
			new RestrictionEnzyme("PasI", "CCCWGGG", 2, 5, ""),
			new RestrictionEnzyme("PauI", "GCGCGC", 1, 5, "BsePI"),
			new RestrictionEnzyme("PceI", "AGGCCT", 3, 3, "StuI"),
			new RestrictionEnzyme("PciI", "ACATGT", 1, 5, "BspLU11I"),
			new RestrictionEnzyme("PciSI", "GCTCTTC", 1, 4, "SapI"),
			new RestrictionEnzyme("PctI", "GAATGC", 1, -1, "BsmI"),
			new RestrictionEnzyme("PdiI", "GCCGGC", 3, 3, "NaeI"),
			new RestrictionEnzyme("PdmI", "GAANNNNTTC", 5, 5, "XmnI"),
			new RestrictionEnzyme("PfeI", "GAWTC", 1, 4, "TfiI"),
			new RestrictionEnzyme("Pfl23II", "CGTACG", 1, 5, "SplI"),
			new RestrictionEnzyme("PflFI", "GACNNNGTC", 4, 5, "Tth111I"),
			new RestrictionEnzyme("PflMI", "CCANNNNNTGG", 7, 4, ""),
			new RestrictionEnzyme("PfoI", "TCCNGGA", 1, 6, ""),
			new RestrictionEnzyme("PhoI", "GGCC", 2, 2, "HaeIII"),
			new RestrictionEnzyme("PinAI", "ACCGGT", 1, 5, "AgeI"),
			new RestrictionEnzyme("PleI", "GAGTC", 4, 5, ""),
			new RestrictionEnzyme("Ple19I", "CGATCG", 4, 2, "PvuI"),
			new RestrictionEnzyme("PmaCI", "CACGTG", 3, 3, ""),
			new RestrictionEnzyme("PmeI", "GTTTAAAC", 4, 4, ""),
			new RestrictionEnzyme("PmlI", "CACGTG", 3, 3, "PmaCI"),
			new RestrictionEnzyme("PpsI", "GAGTC", 4, 5, "PleI"),
			new RestrictionEnzyme("Ppu21I", "YACGTR", 3, 3, "BsaAI"),
			new RestrictionEnzyme("PpuMI", "RGGWCCY", 2, 5, ""),
			new RestrictionEnzyme("PscI", "ACATGT", 1, 5, "BspLU11I"),
			new RestrictionEnzyme("PshAI", "GACNNNNGTC", 5, 5, ""),
			new RestrictionEnzyme("PshBI", "ATTAAT", 2, 4, "VspI"),
			new RestrictionEnzyme("PsiI", "TTATAA", 3, 3, ""),
			new RestrictionEnzyme("Psp5II", "RGGWCCY", 2, 5, "PpuMI"),
			new RestrictionEnzyme("Psp6I", "CCWGG", 0, 5, "EcoRII"),
			new RestrictionEnzyme("Psp1406I", "AACGTT", 2, 4, "AclI"),
			new RestrictionEnzyme("Psp124BI", "GAGCTC", 5, 1, "SacI"),
			new RestrictionEnzyme("PspCI", "CACGTG", 3, 3, "PmaCI"),
			new RestrictionEnzyme("PspEI", "GGTNACC", 1, 6, "BstEII"),
			new RestrictionEnzyme("PspGI", "CCWGG", 0, 5, "EcoRII"),
			new RestrictionEnzyme("PspLI", "CGTACG", 1, 5, "SplI"),
			new RestrictionEnzyme("PspN4I", "GGNNCC", 3, 3, "NlaIV"),
			new RestrictionEnzyme("PspOMI", "GGGCCC", 1, 5, "ApaI"),
			new RestrictionEnzyme("PspPPI", "RGGWCCY", 2, 5, "PpuMI"),
			new RestrictionEnzyme("PspXI", "VCTCGAGB", 2, 6, ""),
			new RestrictionEnzyme("PstI", "CTGCAG", 5, 1, ""),
			new RestrictionEnzyme("PsuI", "RGATCY", 1, 5, "XhoII"),
			new RestrictionEnzyme("PsyI", "GACNNNGTC", 4, 5, "Tth111I"),
			new RestrictionEnzyme("PvuI", "CGATCG", 4, 2, ""),
			new RestrictionEnzyme("PvuII", "CAGCTG", 3, 3, ""),
			new RestrictionEnzyme("RcaI", "TCATGA", 1, 5, "BspHI"),
			new RestrictionEnzyme("RgaI", "GCGATCGC", 5, 3, "SgfI"),
			new RestrictionEnzyme("RigI", "GGCCGGCC", 6, 2, "FseI"),
			new RestrictionEnzyme("RsaI", "GTAC", 2, 2, ""),
			new RestrictionEnzyme("RsaNI", "GTAC", 1, 3, "RsaI"),
			new RestrictionEnzyme("RseI", "CAYNNNNRTG", 5, 5, "MslI"),
			new RestrictionEnzyme("RsrII", "CGGWCCG", 2, 5, ""),
			new RestrictionEnzyme("Rsr2I", "CGGWCCG", 2, 5, "RsrII"),
			new RestrictionEnzyme("SacI", "GAGCTC", 5, 1, ""),
			new RestrictionEnzyme("SacII", "CCGCGG", 4, 2, ""),
			new RestrictionEnzyme("SalI", "GTCGAC", 1, 5, ""),
			new RestrictionEnzyme("SanDI", "GGGWCCC", 2, 5, ""),
			new RestrictionEnzyme("SapI", "GCTCTTC", 1, 4, ""),
			new RestrictionEnzyme("SatI", "GCNGC", 2, 3, "Fnu4HI"),
			new RestrictionEnzyme("Sau96I", "GGNCC", 1, 4, "AsuI"),
			new RestrictionEnzyme("Sau3AI", "GATC", 0, 4, "MboI"),
			new RestrictionEnzyme("SbfI", "CCTGCAGG", 6, 2, "Sse8387I"),
			new RestrictionEnzyme("ScaI", "AGTACT", 3, 3, ""),
			new RestrictionEnzyme("SchI", "GAGTC", 5, 5, "PleI"),
			new RestrictionEnzyme("ScrFI", "CCNGG", 2, 3, ""),
			new RestrictionEnzyme("SdaI", "CCTGCAGG", 6, 2, "Sse8387I"),
			new RestrictionEnzyme("SduI", "GDGCHC", 5, 1, ""),
			new RestrictionEnzyme("SetI", "ASST", 4, 0, ""),
			new RestrictionEnzyme("SexAI", "ACCWGGT", 1, 6, ""),
			new RestrictionEnzyme("SfaAI", "GCGATCGC", 5, 3, "SgfI"),
			new RestrictionEnzyme("SfaNI", "GCATC", 5, 9, ""),
			new RestrictionEnzyme("SfcI", "CTRYAG", 1, 5, "SfeI"),
			new RestrictionEnzyme("SfiI", "GGCCNNNNNGGCC", 8, 5, ""),
			new RestrictionEnzyme("SfoI", "GGCGCC", 3, 3, "NarI"),
			new RestrictionEnzyme("Sfr274I", "CTCGAG", 1, 5, "XhoI"),
			new RestrictionEnzyme("Sfr303I", "CCGCGG", 4, 2, "SacII"),
			new RestrictionEnzyme("SfuI", "TTCGAA", 2, 4, "AsuII"),
			new RestrictionEnzyme("SgfI", "GCGATCGC", 5, 3, ""),
			new RestrictionEnzyme("SgrAI", "CRCCGGYG", 2, 6, ""),
			new RestrictionEnzyme("SgrBI", "CCGCGG", 4, 2, "SacII"),
			new RestrictionEnzyme("SgrDI", "CGTCGACG", 2, 6, ""),
			new RestrictionEnzyme("SgsI", "GGCGCGCC", 2, 6, "AscI"),
			new RestrictionEnzyme("SinI", "GGWCC", 1, 4, "AvaII"),
			new RestrictionEnzyme("SlaI", "CTCGAG", 1, 5, "XhoI"),
			new RestrictionEnzyme("SmaI", "CCCGGG", 3, 3, ""),
			new RestrictionEnzyme("SmiI", "ATTTAAAT", 4, 4, "SwaI"),
			new RestrictionEnzyme("SmiMI", "CAYNNNNRTG", 5, 5, "MslI"),
			new RestrictionEnzyme("SmlI", "CTYRAG", 1, 5, ""),
			new RestrictionEnzyme("SmoI", "CTYRAG", 1, 5, "SmlI"),
			new RestrictionEnzyme("SmuI", "CCCGC", 4, 6, "FauI"),
			new RestrictionEnzyme("SnaBI", "TACGTA", 3, 3, ""),
			new RestrictionEnzyme("SpeI", "ACTAGT", 1, 5, ""),
			new RestrictionEnzyme("SphI", "GCATGC", 5, 1, ""),
			new RestrictionEnzyme("SrfI", "GCCCGGGC", 4, 4, ""),
			new RestrictionEnzyme("Sse9I", "AATT", 0, 4, "TspEI"),
			new RestrictionEnzyme("Sse8387I", "CCTGCAGG", 6, 2, ""),
			new RestrictionEnzyme("SseBI", "AGGCCT", 3, 3, "StuI"),
			new RestrictionEnzyme("SsiI", "CCGC", -3, -1, "AciI"),
			new RestrictionEnzyme("SspI", "AATATT", 3, 3, ""),
			new RestrictionEnzyme("SstI", "GAGCTC", 5, 1, "SacI"),
			new RestrictionEnzyme("SstII", "CCGCGG", 4, 2, "SacII"),
			new RestrictionEnzyme("StrI", "CTCGAG", 1, 5, "XhoI"),
			new RestrictionEnzyme("StuI", "AGGCCT", 3, 3, ""),
			new RestrictionEnzyme("StyI", "CCWWGG", 1, 5, ""),
			new RestrictionEnzyme("StyD4I", "CCNGG", 0, 5, "ScrFI"),
			new RestrictionEnzyme("SwaI", "ATTTAAAT", 4, 4, ""),
			new RestrictionEnzyme("TaaI", "ACNGT", 3, 2, "Tsp4CI"),
			new RestrictionEnzyme("TaiI", "ACGT", 4, 0, "MaeII"),
			new RestrictionEnzyme("TaqI", "TCGA", 1, 3, ""),
			new RestrictionEnzyme("TaqII", "GACCGA", 11, 9, ""),
			new RestrictionEnzyme("TasI", "AATT", 0, 4, "TspEI"),
			new RestrictionEnzyme("TatI", "WGTACW", 1, 5, ""),
			new RestrictionEnzyme("TauI", "GCSGC", 4, 1, ""),
			new RestrictionEnzyme("TfiI", "GAWTC", 1, 4, ""),
			new RestrictionEnzyme("TliI", "CTCGAG", 1, 5, "XhoI"),
			new RestrictionEnzyme("Tru1I", "TTAA", 1, 3, "MseI"),
			new RestrictionEnzyme("Tru9I", "TTAA", 1, 3, "MseI"),
			new RestrictionEnzyme("TscAI", "CASTGNN", 7, 0, "TspRI"),
			new RestrictionEnzyme("TseI", "GCWGC", 1, 4, ""),
			new RestrictionEnzyme("TsoI", "TARCCA", 11, 9, ""),
			new RestrictionEnzyme("Tsp45I", "GTSAC", 0, 5, ""),
			new RestrictionEnzyme("Tsp509I", "AATT", 0, 4, "TspEI"),
			new RestrictionEnzyme("TspDTI", "ATGAA", 11, 9, ""),
			new RestrictionEnzyme("TspEI", "AATT", 0, 4, ""),
			new RestrictionEnzyme("TspGWI", "ACGGA", 11, 9, ""),
			new RestrictionEnzyme("TspMI", "CCCGGG", 1, 5, "SmaI"),
			new RestrictionEnzyme("TspRI", "CASTGNN", 7, 0, ""),
			new RestrictionEnzyme("Tth111I", "GACNNNGTC", 4, 5, ""),
			new RestrictionEnzyme("Van91I", "CCANNNNNTGG", 7, 4, "PflMI"),
			new RestrictionEnzyme("Vha464I", "CTTAAG", 1, 5, "AflII"),
			new RestrictionEnzyme("VneI", "GTGCAC", 1, 5, "ApaLI"),
			new RestrictionEnzyme("VpaK11BI", "GGWCC", 1, 4, "AvaII"),
			new RestrictionEnzyme("VspI", "ATTAAT", 2, 4, ""),
			new RestrictionEnzyme("XagI", "CCTNNNNNAGG", 5, 6, "EcoNI"),
			new RestrictionEnzyme("XapI", "RAATTY", 1, 5, "ApoI"),
			new RestrictionEnzyme("XbaI", "TCTAGA", 1, 5, ""),
			new RestrictionEnzyme("XceI", "RCATGY", 5, 1, "NspI"),
			new RestrictionEnzyme("XcmI", "CCANNNNNNNNNTGG", 8, 7, ""),
			new RestrictionEnzyme("XhoI", "CTCGAG", 1, 5, ""),
			new RestrictionEnzyme("XhoII", "RGATCY", 1, 5, ""),
			new RestrictionEnzyme("XmaI", "CCCGGG", 1, 5, "SmaI"),
			new RestrictionEnzyme("XmaCI", "CCCGGG", 1, 5, "SmaI"),
			new RestrictionEnzyme("XmaJI", "CCTAGG", 1, 5, "AvrII"),
			new RestrictionEnzyme("XmiI", "GTMKAC", 2, 4, "AccI"),
			new RestrictionEnzyme("XmnI", "GAANNNNTTC", 5, 5, ""),
			new RestrictionEnzyme("XspI", "CTAG", 1, 3, "MaeI"),
			new RestrictionEnzyme("ZraI", "GACGTC", 3, 3, "AatII"),
			new RestrictionEnzyme("ZrmI", "AGTACT", 3, 3, "ScaI"),
			new RestrictionEnzyme("Zsp2I", "ATGCAT", 5, 1, "AvaIII")
		};

		#endregion

		#region Property accessors for the enzymes
			public static RestrictionEnzyme AanI { get { return Get("AanI"); } }
			public static RestrictionEnzyme AarI { get { return Get("AarI"); } }
			public static RestrictionEnzyme AasI { get { return Get("AasI"); } }
			public static RestrictionEnzyme AatI { get { return Get("AatI"); } }
			public static RestrictionEnzyme AatII { get { return Get("AatII"); } }
			public static RestrictionEnzyme AbsI { get { return Get("AbsI"); } }
			public static RestrictionEnzyme AccI { get { return Get("AccI"); } }
			public static RestrictionEnzyme AccII { get { return Get("AccII"); } }
			public static RestrictionEnzyme AccIII { get { return Get("AccIII"); } }
			public static RestrictionEnzyme Acc16I { get { return Get("Acc16I"); } }
			public static RestrictionEnzyme Acc36I { get { return Get("Acc36I"); } }
			public static RestrictionEnzyme Acc65I { get { return Get("Acc65I"); } }
			public static RestrictionEnzyme AccB1I { get { return Get("AccB1I"); } }
			public static RestrictionEnzyme AccB7I { get { return Get("AccB7I"); } }
			public static RestrictionEnzyme AccBSI { get { return Get("AccBSI"); } }
			public static RestrictionEnzyme AciI { get { return Get("AciI"); } }
			public static RestrictionEnzyme AclI { get { return Get("AclI"); } }
			public static RestrictionEnzyme AclWI { get { return Get("AclWI"); } }
			public static RestrictionEnzyme AcoI { get { return Get("AcoI"); } }
			public static RestrictionEnzyme AcsI { get { return Get("AcsI"); } }
			public static RestrictionEnzyme AcuI { get { return Get("AcuI"); } }
			public static RestrictionEnzyme AcvI { get { return Get("AcvI"); } }
			public static RestrictionEnzyme AcyI { get { return Get("AcyI"); } }
			public static RestrictionEnzyme AdeI { get { return Get("AdeI"); } }
			public static RestrictionEnzyme AfaI { get { return Get("AfaI"); } }
			public static RestrictionEnzyme AfeI { get { return Get("AfeI"); } }
			public static RestrictionEnzyme AfiI { get { return Get("AfiI"); } }
			public static RestrictionEnzyme AflII { get { return Get("AflII"); } }
			public static RestrictionEnzyme AflIII { get { return Get("AflIII"); } }
			public static RestrictionEnzyme AgeI { get { return Get("AgeI"); } }
			public static RestrictionEnzyme AhdI { get { return Get("AhdI"); } }
			public static RestrictionEnzyme AhlI { get { return Get("AhlI"); } }
			public static RestrictionEnzyme AjiI { get { return Get("AjiI"); } }
			public static RestrictionEnzyme AjnI { get { return Get("AjnI"); } }
			public static RestrictionEnzyme AleI { get { return Get("AleI"); } }
			public static RestrictionEnzyme AluI { get { return Get("AluI"); } }
			public static RestrictionEnzyme AluBI { get { return Get("AluBI"); } }
			public static RestrictionEnzyme AlwI { get { return Get("AlwI"); } }
			public static RestrictionEnzyme Alw21I { get { return Get("Alw21I"); } }
			public static RestrictionEnzyme Alw26I { get { return Get("Alw26I"); } }
			public static RestrictionEnzyme Alw44I { get { return Get("Alw44I"); } }
			public static RestrictionEnzyme AlwNI { get { return Get("AlwNI"); } }
			public static RestrictionEnzyme Ama87I { get { return Get("Ama87I"); } }
			public static RestrictionEnzyme Aor13HI { get { return Get("Aor13HI"); } }
			public static RestrictionEnzyme Aor51HI { get { return Get("Aor51HI"); } }
			public static RestrictionEnzyme ApaI { get { return Get("ApaI"); } }
			public static RestrictionEnzyme ApaLI { get { return Get("ApaLI"); } }
			public static RestrictionEnzyme ApeKI { get { return Get("ApeKI"); } }
			public static RestrictionEnzyme ApoI { get { return Get("ApoI"); } }
			public static RestrictionEnzyme AscI { get { return Get("AscI"); } }
			public static RestrictionEnzyme AseI { get { return Get("AseI"); } }
			public static RestrictionEnzyme AsiGI { get { return Get("AsiGI"); } }
			public static RestrictionEnzyme AsiSI { get { return Get("AsiSI"); } }
			public static RestrictionEnzyme AspI { get { return Get("AspI"); } }
			public static RestrictionEnzyme Asp700I { get { return Get("Asp700I"); } }
			public static RestrictionEnzyme Asp718I { get { return Get("Asp718I"); } }
			public static RestrictionEnzyme AspA2I { get { return Get("AspA2I"); } }
			public static RestrictionEnzyme AspEI { get { return Get("AspEI"); } }
			public static RestrictionEnzyme AspLEI { get { return Get("AspLEI"); } }
			public static RestrictionEnzyme AspS9I { get { return Get("AspS9I"); } }
			public static RestrictionEnzyme AssI { get { return Get("AssI"); } }
			public static RestrictionEnzyme AsuC2I { get { return Get("AsuC2I"); } }
			public static RestrictionEnzyme AsuHPI { get { return Get("AsuHPI"); } }
			public static RestrictionEnzyme AsuNHI { get { return Get("AsuNHI"); } }
			public static RestrictionEnzyme AvaI { get { return Get("AvaI"); } }
			public static RestrictionEnzyme AvaII { get { return Get("AvaII"); } }
			public static RestrictionEnzyme AviII { get { return Get("AviII"); } }
			public static RestrictionEnzyme AvrII { get { return Get("AvrII"); } }
			public static RestrictionEnzyme AxyI { get { return Get("AxyI"); } }
			public static RestrictionEnzyme BaeGI { get { return Get("BaeGI"); } }
			public static RestrictionEnzyme BalI { get { return Get("BalI"); } }
			public static RestrictionEnzyme BamHI { get { return Get("BamHI"); } }
			public static RestrictionEnzyme BanI { get { return Get("BanI"); } }
			public static RestrictionEnzyme BanII { get { return Get("BanII"); } }
			public static RestrictionEnzyme BanIII { get { return Get("BanIII"); } }
			public static RestrictionEnzyme BasI { get { return Get("BasI"); } }
			public static RestrictionEnzyme BauI { get { return Get("BauI"); } }
			public static RestrictionEnzyme BbeI { get { return Get("BbeI"); } }
			public static RestrictionEnzyme BbrPI { get { return Get("BbrPI"); } }
			public static RestrictionEnzyme BbsI { get { return Get("BbsI"); } }
			public static RestrictionEnzyme BbuI { get { return Get("BbuI"); } }
			public static RestrictionEnzyme BbvI { get { return Get("BbvI"); } }
			public static RestrictionEnzyme Bbv12I { get { return Get("Bbv12I"); } }
			public static RestrictionEnzyme BbvCI { get { return Get("BbvCI"); } }
			public static RestrictionEnzyme BccI { get { return Get("BccI"); } }
			public static RestrictionEnzyme BceAI { get { return Get("BceAI"); } }
			public static RestrictionEnzyme BciVI { get { return Get("BciVI"); } }
			public static RestrictionEnzyme BclI { get { return Get("BclI"); } }
			public static RestrictionEnzyme BcnI { get { return Get("BcnI"); } }
			public static RestrictionEnzyme BcuI { get { return Get("BcuI"); } }
			public static RestrictionEnzyme BfaI { get { return Get("BfaI"); } }
			public static RestrictionEnzyme BfiI { get { return Get("BfiI"); } }
			public static RestrictionEnzyme BfmI { get { return Get("BfmI"); } }
			public static RestrictionEnzyme BfrI { get { return Get("BfrI"); } }
			public static RestrictionEnzyme BfuI { get { return Get("BfuI"); } }
			public static RestrictionEnzyme BfuAI { get { return Get("BfuAI"); } }
			public static RestrictionEnzyme BfuCI { get { return Get("BfuCI"); } }
			public static RestrictionEnzyme BglI { get { return Get("BglI"); } }
			public static RestrictionEnzyme BglII { get { return Get("BglII"); } }
			public static RestrictionEnzyme BisI { get { return Get("BisI"); } }
			public static RestrictionEnzyme BlnI { get { return Get("BlnI"); } }
			public static RestrictionEnzyme BlpI { get { return Get("BlpI"); } }
			public static RestrictionEnzyme BlsI { get { return Get("BlsI"); } }
			public static RestrictionEnzyme BmcAI { get { return Get("BmcAI"); } }
			public static RestrictionEnzyme Bme18I { get { return Get("Bme18I"); } }
			public static RestrictionEnzyme Bme1390I { get { return Get("Bme1390I"); } }
			public static RestrictionEnzyme BmeRI { get { return Get("BmeRI"); } }
			public static RestrictionEnzyme BmeT110I { get { return Get("BmeT110I"); } }
			public static RestrictionEnzyme BmgBI { get { return Get("BmgBI"); } }
			public static RestrictionEnzyme BmgT120I { get { return Get("BmgT120I"); } }
			public static RestrictionEnzyme BmiI { get { return Get("BmiI"); } }
			public static RestrictionEnzyme BmrI { get { return Get("BmrI"); } }
			public static RestrictionEnzyme BmrFI { get { return Get("BmrFI"); } }
			public static RestrictionEnzyme BmtI { get { return Get("BmtI"); } }
			public static RestrictionEnzyme BmuI { get { return Get("BmuI"); } }
			public static RestrictionEnzyme BoxI { get { return Get("BoxI"); } }
			public static RestrictionEnzyme BpiI { get { return Get("BpiI"); } }
			public static RestrictionEnzyme BpmI { get { return Get("BpmI"); } }
			public static RestrictionEnzyme Bpu10I { get { return Get("Bpu10I"); } }
			public static RestrictionEnzyme Bpu14I { get { return Get("Bpu14I"); } }
			public static RestrictionEnzyme Bpu1102I { get { return Get("Bpu1102I"); } }
			public static RestrictionEnzyme BpuAI { get { return Get("BpuAI"); } }
			public static RestrictionEnzyme BpuEI { get { return Get("BpuEI"); } }
			public static RestrictionEnzyme BpuMI { get { return Get("BpuMI"); } }
			public static RestrictionEnzyme BpvUI { get { return Get("BpvUI"); } }
			public static RestrictionEnzyme BsaI { get { return Get("BsaI"); } }
			public static RestrictionEnzyme Bsa29I { get { return Get("Bsa29I"); } }
			public static RestrictionEnzyme BsaAI { get { return Get("BsaAI"); } }
			public static RestrictionEnzyme BsaBI { get { return Get("BsaBI"); } }
			public static RestrictionEnzyme BsaHI { get { return Get("BsaHI"); } }
			public static RestrictionEnzyme BsaJI { get { return Get("BsaJI"); } }
			public static RestrictionEnzyme BsaMI { get { return Get("BsaMI"); } }
			public static RestrictionEnzyme BsaWI { get { return Get("BsaWI"); } }
			public static RestrictionEnzyme Bsc4I { get { return Get("Bsc4I"); } }
			public static RestrictionEnzyme Bse1I { get { return Get("Bse1I"); } }
			public static RestrictionEnzyme Bse8I { get { return Get("Bse8I"); } }
			public static RestrictionEnzyme Bse21I { get { return Get("Bse21I"); } }
			public static RestrictionEnzyme Bse118I { get { return Get("Bse118I"); } }
			public static RestrictionEnzyme BseAI { get { return Get("BseAI"); } }
			public static RestrictionEnzyme BseBI { get { return Get("BseBI"); } }
			public static RestrictionEnzyme BseCI { get { return Get("BseCI"); } }
			public static RestrictionEnzyme BseDI { get { return Get("BseDI"); } }
			public static RestrictionEnzyme Bse3DI { get { return Get("Bse3DI"); } }
			public static RestrictionEnzyme BseGI { get { return Get("BseGI"); } }
			public static RestrictionEnzyme BseJI { get { return Get("BseJI"); } }
			public static RestrictionEnzyme BseLI { get { return Get("BseLI"); } }
			public static RestrictionEnzyme BseMI { get { return Get("BseMI"); } }
			public static RestrictionEnzyme BseMII { get { return Get("BseMII"); } }
			public static RestrictionEnzyme BseNI { get { return Get("BseNI"); } }
			public static RestrictionEnzyme BsePI { get { return Get("BsePI"); } }
			public static RestrictionEnzyme BseRI { get { return Get("BseRI"); } }
			public static RestrictionEnzyme BseSI { get { return Get("BseSI"); } }
			public static RestrictionEnzyme BseXI { get { return Get("BseXI"); } }
			public static RestrictionEnzyme BseX3I { get { return Get("BseX3I"); } }
			public static RestrictionEnzyme BseYI { get { return Get("BseYI"); } }
			public static RestrictionEnzyme BsgI { get { return Get("BsgI"); } }
			public static RestrictionEnzyme Bsh1236I { get { return Get("Bsh1236I"); } }
			public static RestrictionEnzyme Bsh1285I { get { return Get("Bsh1285I"); } }
			public static RestrictionEnzyme BshFI { get { return Get("BshFI"); } }
			public static RestrictionEnzyme BshNI { get { return Get("BshNI"); } }
			public static RestrictionEnzyme BshTI { get { return Get("BshTI"); } }
			public static RestrictionEnzyme BshVI { get { return Get("BshVI"); } }
			public static RestrictionEnzyme BsiEI { get { return Get("BsiEI"); } }
			public static RestrictionEnzyme BsiHKAI { get { return Get("BsiHKAI"); } }
			public static RestrictionEnzyme BsiHKCI { get { return Get("BsiHKCI"); } }
			public static RestrictionEnzyme BsiSI { get { return Get("BsiSI"); } }
			public static RestrictionEnzyme BsiWI { get { return Get("BsiWI"); } }
			public static RestrictionEnzyme BslI { get { return Get("BslI"); } }
			public static RestrictionEnzyme BslFI { get { return Get("BslFI"); } }
			public static RestrictionEnzyme BsmI { get { return Get("BsmI"); } }
			public static RestrictionEnzyme BsmAI { get { return Get("BsmAI"); } }
			public static RestrictionEnzyme BsmBI { get { return Get("BsmBI"); } }
			public static RestrictionEnzyme BsmFI { get { return Get("BsmFI"); } }
			public static RestrictionEnzyme BsnI { get { return Get("BsnI"); } }
			public static RestrictionEnzyme Bso31I { get { return Get("Bso31I"); } }
			public static RestrictionEnzyme BsoBI { get { return Get("BsoBI"); } }
			public static RestrictionEnzyme Bsp13I { get { return Get("Bsp13I"); } }
			public static RestrictionEnzyme Bsp19I { get { return Get("Bsp19I"); } }
			public static RestrictionEnzyme Bsp68I { get { return Get("Bsp68I"); } }
			public static RestrictionEnzyme Bsp119I { get { return Get("Bsp119I"); } }
			public static RestrictionEnzyme Bsp120I { get { return Get("Bsp120I"); } }
			public static RestrictionEnzyme Bsp143I { get { return Get("Bsp143I"); } }
			public static RestrictionEnzyme Bsp1286I { get { return Get("Bsp1286I"); } }
			public static RestrictionEnzyme Bsp1407I { get { return Get("Bsp1407I"); } }
			public static RestrictionEnzyme Bsp1720I { get { return Get("Bsp1720I"); } }
			public static RestrictionEnzyme BspACI { get { return Get("BspACI"); } }
			public static RestrictionEnzyme BspANI { get { return Get("BspANI"); } }
			public static RestrictionEnzyme BspCNI { get { return Get("BspCNI"); } }
			public static RestrictionEnzyme BspDI { get { return Get("BspDI"); } }
			public static RestrictionEnzyme BspEI { get { return Get("BspEI"); } }
			public static RestrictionEnzyme BspFNI { get { return Get("BspFNI"); } }
			public static RestrictionEnzyme BspHI { get { return Get("BspHI"); } }
			public static RestrictionEnzyme BspLI { get { return Get("BspLI"); } }
			public static RestrictionEnzyme BspMI { get { return Get("BspMI"); } }
			public static RestrictionEnzyme BspMAI { get { return Get("BspMAI"); } }
			public static RestrictionEnzyme BspOI { get { return Get("BspOI"); } }
			public static RestrictionEnzyme BspPI { get { return Get("BspPI"); } }
			public static RestrictionEnzyme BspQI { get { return Get("BspQI"); } }
			public static RestrictionEnzyme BspTI { get { return Get("BspTI"); } }
			public static RestrictionEnzyme BspT104I { get { return Get("BspT104I"); } }
			public static RestrictionEnzyme BspT107I { get { return Get("BspT107I"); } }
			public static RestrictionEnzyme BspTNI { get { return Get("BspTNI"); } }
			public static RestrictionEnzyme BspXI { get { return Get("BspXI"); } }
			public static RestrictionEnzyme BsrI { get { return Get("BsrI"); } }
			public static RestrictionEnzyme BsrBI { get { return Get("BsrBI"); } }
			public static RestrictionEnzyme BsrDI { get { return Get("BsrDI"); } }
			public static RestrictionEnzyme BsrFI { get { return Get("BsrFI"); } }
			public static RestrictionEnzyme BsrGI { get { return Get("BsrGI"); } }
			public static RestrictionEnzyme BsrSI { get { return Get("BsrSI"); } }
			public static RestrictionEnzyme BssAI { get { return Get("BssAI"); } }
			public static RestrictionEnzyme BssECI { get { return Get("BssECI"); } }
			public static RestrictionEnzyme BssHII { get { return Get("BssHII"); } }
			public static RestrictionEnzyme BssKI { get { return Get("BssKI"); } }
			public static RestrictionEnzyme BssMI { get { return Get("BssMI"); } }
			public static RestrictionEnzyme BssNI { get { return Get("BssNI"); } }
			public static RestrictionEnzyme BssNAI { get { return Get("BssNAI"); } }
			public static RestrictionEnzyme BssSI { get { return Get("BssSI"); } }
			public static RestrictionEnzyme BssT1I { get { return Get("BssT1I"); } }
			public static RestrictionEnzyme Bst6I { get { return Get("Bst6I"); } }
			public static RestrictionEnzyme Bst98I { get { return Get("Bst98I"); } }
			public static RestrictionEnzyme Bst1107I { get { return Get("Bst1107I"); } }
			public static RestrictionEnzyme BstACI { get { return Get("BstACI"); } }
			public static RestrictionEnzyme BstAFI { get { return Get("BstAFI"); } }
			public static RestrictionEnzyme BstAPI { get { return Get("BstAPI"); } }
			public static RestrictionEnzyme BstAUI { get { return Get("BstAUI"); } }
			public static RestrictionEnzyme BstBI { get { return Get("BstBI"); } }
			public static RestrictionEnzyme Bst2BI { get { return Get("Bst2BI"); } }
			public static RestrictionEnzyme BstBAI { get { return Get("BstBAI"); } }
			public static RestrictionEnzyme Bst4CI { get { return Get("Bst4CI"); } }
			public static RestrictionEnzyme BstC8I { get { return Get("BstC8I"); } }
			public static RestrictionEnzyme BstDEI { get { return Get("BstDEI"); } }
			public static RestrictionEnzyme BstDSI { get { return Get("BstDSI"); } }
			public static RestrictionEnzyme BstEII { get { return Get("BstEII"); } }
			public static RestrictionEnzyme BstENI { get { return Get("BstENI"); } }
			public static RestrictionEnzyme BstF5I { get { return Get("BstF5I"); } }
			public static RestrictionEnzyme BstFNI { get { return Get("BstFNI"); } }
			public static RestrictionEnzyme BstH2I { get { return Get("BstH2I"); } }
			public static RestrictionEnzyme BstHHI { get { return Get("BstHHI"); } }
			public static RestrictionEnzyme BstKTI { get { return Get("BstKTI"); } }
			public static RestrictionEnzyme BstMAI { get { return Get("BstMAI"); } }
			public static RestrictionEnzyme BstMBI { get { return Get("BstMBI"); } }
			public static RestrictionEnzyme BstMCI { get { return Get("BstMCI"); } }
			public static RestrictionEnzyme BstMWI { get { return Get("BstMWI"); } }
			public static RestrictionEnzyme BstNI { get { return Get("BstNI"); } }
			public static RestrictionEnzyme BstNSI { get { return Get("BstNSI"); } }
			public static RestrictionEnzyme BstOI { get { return Get("BstOI"); } }
			public static RestrictionEnzyme BstPI { get { return Get("BstPI"); } }
			public static RestrictionEnzyme BstPAI { get { return Get("BstPAI"); } }
			public static RestrictionEnzyme BstSCI { get { return Get("BstSCI"); } }
			public static RestrictionEnzyme BstSFI { get { return Get("BstSFI"); } }
			public static RestrictionEnzyme BstSLI { get { return Get("BstSLI"); } }
			public static RestrictionEnzyme BstSNI { get { return Get("BstSNI"); } }
			public static RestrictionEnzyme BstUI { get { return Get("BstUI"); } }
			public static RestrictionEnzyme Bst2UI { get { return Get("Bst2UI"); } }
			public static RestrictionEnzyme BstV1I { get { return Get("BstV1I"); } }
			public static RestrictionEnzyme BstV2I { get { return Get("BstV2I"); } }
			public static RestrictionEnzyme BstXI { get { return Get("BstXI"); } }
			public static RestrictionEnzyme BstX2I { get { return Get("BstX2I"); } }
			public static RestrictionEnzyme BstYI { get { return Get("BstYI"); } }
			public static RestrictionEnzyme BstZI { get { return Get("BstZI"); } }
			public static RestrictionEnzyme BstZ17I { get { return Get("BstZ17I"); } }
			public static RestrictionEnzyme Bsu15I { get { return Get("Bsu15I"); } }
			public static RestrictionEnzyme Bsu36I { get { return Get("Bsu36I"); } }
			public static RestrictionEnzyme BsuRI { get { return Get("BsuRI"); } }
			public static RestrictionEnzyme BsuTUI { get { return Get("BsuTUI"); } }
			public static RestrictionEnzyme BtgI { get { return Get("BtgI"); } }
			public static RestrictionEnzyme BtgZI { get { return Get("BtgZI"); } }
			public static RestrictionEnzyme BtrI { get { return Get("BtrI"); } }
			public static RestrictionEnzyme BtsI { get { return Get("BtsI"); } }
			public static RestrictionEnzyme BtsCI { get { return Get("BtsCI"); } }
			public static RestrictionEnzyme BtuMI { get { return Get("BtuMI"); } }
			public static RestrictionEnzyme BveI { get { return Get("BveI"); } }
			public static RestrictionEnzyme Cac8I { get { return Get("Cac8I"); } }
			public static RestrictionEnzyme CaiI { get { return Get("CaiI"); } }
			public static RestrictionEnzyme CciI { get { return Get("CciI"); } }
			public static RestrictionEnzyme CciNI { get { return Get("CciNI"); } }
			public static RestrictionEnzyme CelII { get { return Get("CelII"); } }
			public static RestrictionEnzyme CfoI { get { return Get("CfoI"); } }
			public static RestrictionEnzyme CfrI { get { return Get("CfrI"); } }
			public static RestrictionEnzyme Cfr9I { get { return Get("Cfr9I"); } }
			public static RestrictionEnzyme Cfr10I { get { return Get("Cfr10I"); } }
			public static RestrictionEnzyme Cfr13I { get { return Get("Cfr13I"); } }
			public static RestrictionEnzyme Cfr42I { get { return Get("Cfr42I"); } }
			public static RestrictionEnzyme ClaI { get { return Get("ClaI"); } }
			public static RestrictionEnzyme CpoI { get { return Get("CpoI"); } }
			public static RestrictionEnzyme CseI { get { return Get("CseI"); } }
			public static RestrictionEnzyme CspI { get { return Get("CspI"); } }
			public static RestrictionEnzyme Csp6I { get { return Get("Csp6I"); } }
			public static RestrictionEnzyme Csp45I { get { return Get("Csp45I"); } }
			public static RestrictionEnzyme CspAI { get { return Get("CspAI"); } }
			public static RestrictionEnzyme CviAII { get { return Get("CviAII"); } }
			public static RestrictionEnzyme CviJI { get { return Get("CviJI"); } }
			public static RestrictionEnzyme CviKI1 { get { return Get("CviKI1"); } }
			public static RestrictionEnzyme CviQI { get { return Get("CviQI"); } }
			public static RestrictionEnzyme DdeI { get { return Get("DdeI"); } }
			public static RestrictionEnzyme DinI { get { return Get("DinI"); } }
			public static RestrictionEnzyme DpnI { get { return Get("DpnI"); } }
			public static RestrictionEnzyme DpnII { get { return Get("DpnII"); } }
			public static RestrictionEnzyme DraI { get { return Get("DraI"); } }
			public static RestrictionEnzyme DraII { get { return Get("DraII"); } }
			public static RestrictionEnzyme DraIII { get { return Get("DraIII"); } }
			public static RestrictionEnzyme DrdI { get { return Get("DrdI"); } }
			public static RestrictionEnzyme DriI { get { return Get("DriI"); } }
			public static RestrictionEnzyme DseDI { get { return Get("DseDI"); } }
			public static RestrictionEnzyme EaeI { get { return Get("EaeI"); } }
			public static RestrictionEnzyme EagI { get { return Get("EagI"); } }
			public static RestrictionEnzyme Eam1104I { get { return Get("Eam1104I"); } }
			public static RestrictionEnzyme Eam1105I { get { return Get("Eam1105I"); } }
			public static RestrictionEnzyme EarI { get { return Get("EarI"); } }
			public static RestrictionEnzyme EciI { get { return Get("EciI"); } }
			public static RestrictionEnzyme Ecl136II { get { return Get("Ecl136II"); } }
			public static RestrictionEnzyme EclHKI { get { return Get("EclHKI"); } }
			public static RestrictionEnzyme EclXI { get { return Get("EclXI"); } }
			public static RestrictionEnzyme Eco24I { get { return Get("Eco24I"); } }
			public static RestrictionEnzyme Eco31I { get { return Get("Eco31I"); } }
			public static RestrictionEnzyme Eco32I { get { return Get("Eco32I"); } }
			public static RestrictionEnzyme Eco47I { get { return Get("Eco47I"); } }
			public static RestrictionEnzyme Eco47III { get { return Get("Eco47III"); } }
			public static RestrictionEnzyme Eco52I { get { return Get("Eco52I"); } }
			public static RestrictionEnzyme Eco57I { get { return Get("Eco57I"); } }
			public static RestrictionEnzyme Eco72I { get { return Get("Eco72I"); } }
			public static RestrictionEnzyme Eco81I { get { return Get("Eco81I"); } }
			public static RestrictionEnzyme Eco88I { get { return Get("Eco88I"); } }
			public static RestrictionEnzyme Eco91I { get { return Get("Eco91I"); } }
			public static RestrictionEnzyme Eco105I { get { return Get("Eco105I"); } }
			public static RestrictionEnzyme Eco130I { get { return Get("Eco130I"); } }
			public static RestrictionEnzyme Eco147I { get { return Get("Eco147I"); } }
			public static RestrictionEnzyme EcoICRI { get { return Get("EcoICRI"); } }
			public static RestrictionEnzyme Eco57MI { get { return Get("Eco57MI"); } }
			public static RestrictionEnzyme EcoNI { get { return Get("EcoNI"); } }
			public static RestrictionEnzyme EcoO65I { get { return Get("EcoO65I"); } }
			public static RestrictionEnzyme EcoO109I { get { return Get("EcoO109I"); } }
			public static RestrictionEnzyme EcoRI { get { return Get("EcoRI"); } }
			public static RestrictionEnzyme EcoRII { get { return Get("EcoRII"); } }
			public static RestrictionEnzyme EcoRV { get { return Get("EcoRV"); } }
			public static RestrictionEnzyme EcoT14I { get { return Get("EcoT14I"); } }
			public static RestrictionEnzyme EcoT22I { get { return Get("EcoT22I"); } }
			public static RestrictionEnzyme EcoT38I { get { return Get("EcoT38I"); } }
			public static RestrictionEnzyme EgeI { get { return Get("EgeI"); } }
			public static RestrictionEnzyme EheI { get { return Get("EheI"); } }
			public static RestrictionEnzyme ErhI { get { return Get("ErhI"); } }
			public static RestrictionEnzyme Esp3I { get { return Get("Esp3I"); } }
			public static RestrictionEnzyme FaeI { get { return Get("FaeI"); } }
			public static RestrictionEnzyme FaqI { get { return Get("FaqI"); } }
			public static RestrictionEnzyme FatI { get { return Get("FatI"); } }
			public static RestrictionEnzyme FauI { get { return Get("FauI"); } }
			public static RestrictionEnzyme FauNDI { get { return Get("FauNDI"); } }
			public static RestrictionEnzyme FbaI { get { return Get("FbaI"); } }
			public static RestrictionEnzyme FblI { get { return Get("FblI"); } }
			public static RestrictionEnzyme Fnu4HI { get { return Get("Fnu4HI"); } }
			public static RestrictionEnzyme FokI { get { return Get("FokI"); } }
			public static RestrictionEnzyme FriOI { get { return Get("FriOI"); } }
			public static RestrictionEnzyme FseI { get { return Get("FseI"); } }
			public static RestrictionEnzyme FspI { get { return Get("FspI"); } }
			public static RestrictionEnzyme FspAI { get { return Get("FspAI"); } }
			public static RestrictionEnzyme FspBI { get { return Get("FspBI"); } }
			public static RestrictionEnzyme Fsp4HI { get { return Get("Fsp4HI"); } }
			public static RestrictionEnzyme GlaI { get { return Get("GlaI"); } }
			public static RestrictionEnzyme GluI { get { return Get("GluI"); } }
			public static RestrictionEnzyme GsaI { get { return Get("GsaI"); } }
			public static RestrictionEnzyme GsuI { get { return Get("GsuI"); } }
			public static RestrictionEnzyme HaeII { get { return Get("HaeII"); } }
			public static RestrictionEnzyme HaeIII { get { return Get("HaeIII"); } }
			public static RestrictionEnzyme HapII { get { return Get("HapII"); } }
			public static RestrictionEnzyme HgaI { get { return Get("HgaI"); } }
			public static RestrictionEnzyme HhaI { get { return Get("HhaI"); } }
			public static RestrictionEnzyme Hin1I { get { return Get("Hin1I"); } }
			public static RestrictionEnzyme Hin1II { get { return Get("Hin1II"); } }
			public static RestrictionEnzyme Hin6I { get { return Get("Hin6I"); } }
			public static RestrictionEnzyme HinP1I { get { return Get("HinP1I"); } }
			public static RestrictionEnzyme HincII { get { return Get("HincII"); } }
			public static RestrictionEnzyme HindII { get { return Get("HindII"); } }
			public static RestrictionEnzyme HindIII { get { return Get("HindIII"); } }
			public static RestrictionEnzyme HinfI { get { return Get("HinfI"); } }
			public static RestrictionEnzyme HpaI { get { return Get("HpaI"); } }
			public static RestrictionEnzyme HpaII { get { return Get("HpaII"); } }
			public static RestrictionEnzyme HphI { get { return Get("HphI"); } }
			public static RestrictionEnzyme Hpy8I { get { return Get("Hpy8I"); } }
			public static RestrictionEnzyme Hpy99I { get { return Get("Hpy99I"); } }
			public static RestrictionEnzyme Hpy166II { get { return Get("Hpy166II"); } }
			public static RestrictionEnzyme Hpy188I { get { return Get("Hpy188I"); } }
			public static RestrictionEnzyme Hpy188III { get { return Get("Hpy188III"); } }
			public static RestrictionEnzyme HpyAV { get { return Get("HpyAV"); } }
			public static RestrictionEnzyme HpyCH4III { get { return Get("HpyCH4III"); } }
			public static RestrictionEnzyme HpyCH4IV { get { return Get("HpyCH4IV"); } }
			public static RestrictionEnzyme HpyCH4V { get { return Get("HpyCH4V"); } }
			public static RestrictionEnzyme HpyF3I { get { return Get("HpyF3I"); } }
			public static RestrictionEnzyme HpyF10VI { get { return Get("HpyF10VI"); } }
			public static RestrictionEnzyme Hsp92I { get { return Get("Hsp92I"); } }
			public static RestrictionEnzyme Hsp92II { get { return Get("Hsp92II"); } }
			public static RestrictionEnzyme HspAI { get { return Get("HspAI"); } }
			public static RestrictionEnzyme ItaI { get { return Get("ItaI"); } }
			public static RestrictionEnzyme KasI { get { return Get("KasI"); } }
			public static RestrictionEnzyme KpnI { get { return Get("KpnI"); } }
			public static RestrictionEnzyme Kpn2I { get { return Get("Kpn2I"); } }
			public static RestrictionEnzyme KspI { get { return Get("KspI"); } }
			public static RestrictionEnzyme Ksp22I { get { return Get("Ksp22I"); } }
			public static RestrictionEnzyme KspAI { get { return Get("KspAI"); } }
			public static RestrictionEnzyme Kzo9I { get { return Get("Kzo9I"); } }
			public static RestrictionEnzyme LguI { get { return Get("LguI"); } }
			public static RestrictionEnzyme Lsp1109I { get { return Get("Lsp1109I"); } }
			public static RestrictionEnzyme LweI { get { return Get("LweI"); } }
			public static RestrictionEnzyme MabI { get { return Get("MabI"); } }
			public static RestrictionEnzyme MaeI { get { return Get("MaeI"); } }
			public static RestrictionEnzyme MaeII { get { return Get("MaeII"); } }
			public static RestrictionEnzyme MaeIII { get { return Get("MaeIII"); } }
			public static RestrictionEnzyme MalI { get { return Get("MalI"); } }
			public static RestrictionEnzyme MauBI { get { return Get("MauBI"); } }
			public static RestrictionEnzyme MbiI { get { return Get("MbiI"); } }
			public static RestrictionEnzyme MboI { get { return Get("MboI"); } }
			public static RestrictionEnzyme MboII { get { return Get("MboII"); } }
			public static RestrictionEnzyme MfeI { get { return Get("MfeI"); } }
			public static RestrictionEnzyme MflI { get { return Get("MflI"); } }
			public static RestrictionEnzyme MhlI { get { return Get("MhlI"); } }
			public static RestrictionEnzyme MlsI { get { return Get("MlsI"); } }
			public static RestrictionEnzyme MluI { get { return Get("MluI"); } }
			public static RestrictionEnzyme MluNI { get { return Get("MluNI"); } }
			public static RestrictionEnzyme MlyI { get { return Get("MlyI"); } }
			public static RestrictionEnzyme Mly113I { get { return Get("Mly113I"); } }
			public static RestrictionEnzyme MmeI { get { return Get("MmeI"); } }
			public static RestrictionEnzyme MnlI { get { return Get("MnlI"); } }
			public static RestrictionEnzyme Mph1103I { get { return Get("Mph1103I"); } }
			public static RestrictionEnzyme MreI { get { return Get("MreI"); } }
			public static RestrictionEnzyme MroI { get { return Get("MroI"); } }
			public static RestrictionEnzyme MroNI { get { return Get("MroNI"); } }
			public static RestrictionEnzyme MroXI { get { return Get("MroXI"); } }
			public static RestrictionEnzyme MscI { get { return Get("MscI"); } }
			public static RestrictionEnzyme MseI { get { return Get("MseI"); } }
			public static RestrictionEnzyme MslI { get { return Get("MslI"); } }
			public static RestrictionEnzyme MspI { get { return Get("MspI"); } }
			public static RestrictionEnzyme Msp20I { get { return Get("Msp20I"); } }
			public static RestrictionEnzyme MspA1I { get { return Get("MspA1I"); } }
			public static RestrictionEnzyme MspCI { get { return Get("MspCI"); } }
			public static RestrictionEnzyme MspR9I { get { return Get("MspR9I"); } }
			public static RestrictionEnzyme MssI { get { return Get("MssI"); } }
			public static RestrictionEnzyme MunI { get { return Get("MunI"); } }
			public static RestrictionEnzyme MvaI { get { return Get("MvaI"); } }
			public static RestrictionEnzyme Mva1269I { get { return Get("Mva1269I"); } }
			public static RestrictionEnzyme MvnI { get { return Get("MvnI"); } }
			public static RestrictionEnzyme MvrI { get { return Get("MvrI"); } }
			public static RestrictionEnzyme MwoI { get { return Get("MwoI"); } }
			public static RestrictionEnzyme NaeI { get { return Get("NaeI"); } }
			public static RestrictionEnzyme NarI { get { return Get("NarI"); } }
			public static RestrictionEnzyme NciI { get { return Get("NciI"); } }
			public static RestrictionEnzyme NcoI { get { return Get("NcoI"); } }
			public static RestrictionEnzyme NdeI { get { return Get("NdeI"); } }
			public static RestrictionEnzyme NdeII { get { return Get("NdeII"); } }
			public static RestrictionEnzyme NgoMIV { get { return Get("NgoMIV"); } }
			public static RestrictionEnzyme NheI { get { return Get("NheI"); } }
			public static RestrictionEnzyme NlaIII { get { return Get("NlaIII"); } }
			public static RestrictionEnzyme NlaIV { get { return Get("NlaIV"); } }
			public static RestrictionEnzyme NmeAIII { get { return Get("NmeAIII"); } }
			public static RestrictionEnzyme NmuCI { get { return Get("NmuCI"); } }
			public static RestrictionEnzyme NotI { get { return Get("NotI"); } }
			public static RestrictionEnzyme NruI { get { return Get("NruI"); } }
			public static RestrictionEnzyme NsbI { get { return Get("NsbI"); } }
			public static RestrictionEnzyme NsiI { get { return Get("NsiI"); } }
			public static RestrictionEnzyme NspI { get { return Get("NspI"); } }
			public static RestrictionEnzyme NspV { get { return Get("NspV"); } }
			public static RestrictionEnzyme OliI { get { return Get("OliI"); } }
			public static RestrictionEnzyme PacI { get { return Get("PacI"); } }
			public static RestrictionEnzyme PaeI { get { return Get("PaeI"); } }
			public static RestrictionEnzyme PaeR7I { get { return Get("PaeR7I"); } }
			public static RestrictionEnzyme PagI { get { return Get("PagI"); } }
			public static RestrictionEnzyme PalAI { get { return Get("PalAI"); } }
			public static RestrictionEnzyme PasI { get { return Get("PasI"); } }
			public static RestrictionEnzyme PauI { get { return Get("PauI"); } }
			public static RestrictionEnzyme PceI { get { return Get("PceI"); } }
			public static RestrictionEnzyme PciI { get { return Get("PciI"); } }
			public static RestrictionEnzyme PciSI { get { return Get("PciSI"); } }
			public static RestrictionEnzyme PctI { get { return Get("PctI"); } }
			public static RestrictionEnzyme PdiI { get { return Get("PdiI"); } }
			public static RestrictionEnzyme PdmI { get { return Get("PdmI"); } }
			public static RestrictionEnzyme PfeI { get { return Get("PfeI"); } }
			public static RestrictionEnzyme Pfl23II { get { return Get("Pfl23II"); } }
			public static RestrictionEnzyme PflFI { get { return Get("PflFI"); } }
			public static RestrictionEnzyme PflMI { get { return Get("PflMI"); } }
			public static RestrictionEnzyme PfoI { get { return Get("PfoI"); } }
			public static RestrictionEnzyme PhoI { get { return Get("PhoI"); } }
			public static RestrictionEnzyme PinAI { get { return Get("PinAI"); } }
			public static RestrictionEnzyme PleI { get { return Get("PleI"); } }
			public static RestrictionEnzyme Ple19I { get { return Get("Ple19I"); } }
			public static RestrictionEnzyme PmaCI { get { return Get("PmaCI"); } }
			public static RestrictionEnzyme PmeI { get { return Get("PmeI"); } }
			public static RestrictionEnzyme PmlI { get { return Get("PmlI"); } }
			public static RestrictionEnzyme PpsI { get { return Get("PpsI"); } }
			public static RestrictionEnzyme Ppu21I { get { return Get("Ppu21I"); } }
			public static RestrictionEnzyme PpuMI { get { return Get("PpuMI"); } }
			public static RestrictionEnzyme PscI { get { return Get("PscI"); } }
			public static RestrictionEnzyme PshAI { get { return Get("PshAI"); } }
			public static RestrictionEnzyme PshBI { get { return Get("PshBI"); } }
			public static RestrictionEnzyme PsiI { get { return Get("PsiI"); } }
			public static RestrictionEnzyme Psp5II { get { return Get("Psp5II"); } }
			public static RestrictionEnzyme Psp6I { get { return Get("Psp6I"); } }
			public static RestrictionEnzyme Psp1406I { get { return Get("Psp1406I"); } }
			public static RestrictionEnzyme Psp124BI { get { return Get("Psp124BI"); } }
			public static RestrictionEnzyme PspCI { get { return Get("PspCI"); } }
			public static RestrictionEnzyme PspEI { get { return Get("PspEI"); } }
			public static RestrictionEnzyme PspGI { get { return Get("PspGI"); } }
			public static RestrictionEnzyme PspLI { get { return Get("PspLI"); } }
			public static RestrictionEnzyme PspN4I { get { return Get("PspN4I"); } }
			public static RestrictionEnzyme PspOMI { get { return Get("PspOMI"); } }
			public static RestrictionEnzyme PspPPI { get { return Get("PspPPI"); } }
			public static RestrictionEnzyme PspXI { get { return Get("PspXI"); } }
			public static RestrictionEnzyme PstI { get { return Get("PstI"); } }
			public static RestrictionEnzyme PsuI { get { return Get("PsuI"); } }
			public static RestrictionEnzyme PsyI { get { return Get("PsyI"); } }
			public static RestrictionEnzyme PvuI { get { return Get("PvuI"); } }
			public static RestrictionEnzyme PvuII { get { return Get("PvuII"); } }
			public static RestrictionEnzyme RcaI { get { return Get("RcaI"); } }
			public static RestrictionEnzyme RgaI { get { return Get("RgaI"); } }
			public static RestrictionEnzyme RigI { get { return Get("RigI"); } }
			public static RestrictionEnzyme RsaI { get { return Get("RsaI"); } }
			public static RestrictionEnzyme RsaNI { get { return Get("RsaNI"); } }
			public static RestrictionEnzyme RseI { get { return Get("RseI"); } }
			public static RestrictionEnzyme RsrII { get { return Get("RsrII"); } }
			public static RestrictionEnzyme Rsr2I { get { return Get("Rsr2I"); } }
			public static RestrictionEnzyme SacI { get { return Get("SacI"); } }
			public static RestrictionEnzyme SacII { get { return Get("SacII"); } }
			public static RestrictionEnzyme SalI { get { return Get("SalI"); } }
			public static RestrictionEnzyme SanDI { get { return Get("SanDI"); } }
			public static RestrictionEnzyme SapI { get { return Get("SapI"); } }
			public static RestrictionEnzyme SatI { get { return Get("SatI"); } }
			public static RestrictionEnzyme Sau96I { get { return Get("Sau96I"); } }
			public static RestrictionEnzyme Sau3AI { get { return Get("Sau3AI"); } }
			public static RestrictionEnzyme SbfI { get { return Get("SbfI"); } }
			public static RestrictionEnzyme ScaI { get { return Get("ScaI"); } }
			public static RestrictionEnzyme SchI { get { return Get("SchI"); } }
			public static RestrictionEnzyme ScrFI { get { return Get("ScrFI"); } }
			public static RestrictionEnzyme SdaI { get { return Get("SdaI"); } }
			public static RestrictionEnzyme SduI { get { return Get("SduI"); } }
			public static RestrictionEnzyme SetI { get { return Get("SetI"); } }
			public static RestrictionEnzyme SexAI { get { return Get("SexAI"); } }
			public static RestrictionEnzyme SfaAI { get { return Get("SfaAI"); } }
			public static RestrictionEnzyme SfaNI { get { return Get("SfaNI"); } }
			public static RestrictionEnzyme SfcI { get { return Get("SfcI"); } }
			public static RestrictionEnzyme SfiI { get { return Get("SfiI"); } }
			public static RestrictionEnzyme SfoI { get { return Get("SfoI"); } }
			public static RestrictionEnzyme Sfr274I { get { return Get("Sfr274I"); } }
			public static RestrictionEnzyme Sfr303I { get { return Get("Sfr303I"); } }
			public static RestrictionEnzyme SfuI { get { return Get("SfuI"); } }
			public static RestrictionEnzyme SgfI { get { return Get("SgfI"); } }
			public static RestrictionEnzyme SgrAI { get { return Get("SgrAI"); } }
			public static RestrictionEnzyme SgrBI { get { return Get("SgrBI"); } }
			public static RestrictionEnzyme SgrDI { get { return Get("SgrDI"); } }
			public static RestrictionEnzyme SgsI { get { return Get("SgsI"); } }
			public static RestrictionEnzyme SinI { get { return Get("SinI"); } }
			public static RestrictionEnzyme SlaI { get { return Get("SlaI"); } }
			public static RestrictionEnzyme SmaI { get { return Get("SmaI"); } }
			public static RestrictionEnzyme SmiI { get { return Get("SmiI"); } }
			public static RestrictionEnzyme SmiMI { get { return Get("SmiMI"); } }
			public static RestrictionEnzyme SmlI { get { return Get("SmlI"); } }
			public static RestrictionEnzyme SmoI { get { return Get("SmoI"); } }
			public static RestrictionEnzyme SmuI { get { return Get("SmuI"); } }
			public static RestrictionEnzyme SnaBI { get { return Get("SnaBI"); } }
			public static RestrictionEnzyme SpeI { get { return Get("SpeI"); } }
			public static RestrictionEnzyme SphI { get { return Get("SphI"); } }
			public static RestrictionEnzyme SrfI { get { return Get("SrfI"); } }
			public static RestrictionEnzyme Sse9I { get { return Get("Sse9I"); } }
			public static RestrictionEnzyme Sse8387I { get { return Get("Sse8387I"); } }
			public static RestrictionEnzyme SseBI { get { return Get("SseBI"); } }
			public static RestrictionEnzyme SsiI { get { return Get("SsiI"); } }
			public static RestrictionEnzyme SspI { get { return Get("SspI"); } }
			public static RestrictionEnzyme SstI { get { return Get("SstI"); } }
			public static RestrictionEnzyme SstII { get { return Get("SstII"); } }
			public static RestrictionEnzyme StrI { get { return Get("StrI"); } }
			public static RestrictionEnzyme StuI { get { return Get("StuI"); } }
			public static RestrictionEnzyme StyI { get { return Get("StyI"); } }
			public static RestrictionEnzyme StyD4I { get { return Get("StyD4I"); } }
			public static RestrictionEnzyme SwaI { get { return Get("SwaI"); } }
			public static RestrictionEnzyme TaaI { get { return Get("TaaI"); } }
			public static RestrictionEnzyme TaiI { get { return Get("TaiI"); } }
			public static RestrictionEnzyme TaqI { get { return Get("TaqI"); } }
			public static RestrictionEnzyme TaqII { get { return Get("TaqII"); } }
			public static RestrictionEnzyme TasI { get { return Get("TasI"); } }
			public static RestrictionEnzyme TatI { get { return Get("TatI"); } }
			public static RestrictionEnzyme TauI { get { return Get("TauI"); } }
			public static RestrictionEnzyme TfiI { get { return Get("TfiI"); } }
			public static RestrictionEnzyme TliI { get { return Get("TliI"); } }
			public static RestrictionEnzyme Tru1I { get { return Get("Tru1I"); } }
			public static RestrictionEnzyme Tru9I { get { return Get("Tru9I"); } }
			public static RestrictionEnzyme TscAI { get { return Get("TscAI"); } }
			public static RestrictionEnzyme TseI { get { return Get("TseI"); } }
			public static RestrictionEnzyme TsoI { get { return Get("TsoI"); } }
			public static RestrictionEnzyme Tsp45I { get { return Get("Tsp45I"); } }
			public static RestrictionEnzyme Tsp509I { get { return Get("Tsp509I"); } }
			public static RestrictionEnzyme TspDTI { get { return Get("TspDTI"); } }
			public static RestrictionEnzyme TspEI { get { return Get("TspEI"); } }
			public static RestrictionEnzyme TspGWI { get { return Get("TspGWI"); } }
			public static RestrictionEnzyme TspMI { get { return Get("TspMI"); } }
			public static RestrictionEnzyme TspRI { get { return Get("TspRI"); } }
			public static RestrictionEnzyme Tth111I { get { return Get("Tth111I"); } }
			public static RestrictionEnzyme Van91I { get { return Get("Van91I"); } }
			public static RestrictionEnzyme Vha464I { get { return Get("Vha464I"); } }
			public static RestrictionEnzyme VneI { get { return Get("VneI"); } }
			public static RestrictionEnzyme VpaK11BI { get { return Get("VpaK11BI"); } }
			public static RestrictionEnzyme VspI { get { return Get("VspI"); } }
			public static RestrictionEnzyme XagI { get { return Get("XagI"); } }
			public static RestrictionEnzyme XapI { get { return Get("XapI"); } }
			public static RestrictionEnzyme XbaI { get { return Get("XbaI"); } }
			public static RestrictionEnzyme XceI { get { return Get("XceI"); } }
			public static RestrictionEnzyme XcmI { get { return Get("XcmI"); } }
			public static RestrictionEnzyme XhoI { get { return Get("XhoI"); } }
			public static RestrictionEnzyme XhoII { get { return Get("XhoII"); } }
			public static RestrictionEnzyme XmaI { get { return Get("XmaI"); } }
			public static RestrictionEnzyme XmaCI { get { return Get("XmaCI"); } }
			public static RestrictionEnzyme XmaJI { get { return Get("XmaJI"); } }
			public static RestrictionEnzyme XmiI { get { return Get("XmiI"); } }
			public static RestrictionEnzyme XmnI { get { return Get("XmnI"); } }
			public static RestrictionEnzyme XspI { get { return Get("XspI"); } }
			public static RestrictionEnzyme ZraI { get { return Get("ZraI"); } }
			public static RestrictionEnzyme ZrmI { get { return Get("ZrmI"); } }
			public static RestrictionEnzyme Zsp2I { get { return Get("Zsp2I"); } }
		#endregion

		static RestrictionEnzymes()
		{
			foreach(RestrictionEnzyme re in All) enzymeDictionary.Add(re.Name, re);
		}

		private static Dictionary<string, RestrictionEnzyme> enzymeDictionary = new Dictionary<string, RestrictionEnzyme>();
		public static RestrictionEnzyme Get(string name)
		{
			return enzymeDictionary[name];
		}
	}

	public class RestrictionEnzyme
	{
		public string Name { get; set; }
		public DnaSequence Sequence { get; set; }
		public int UpperCutOffset { get; set; }
		public int LowerCutOffset { get; set; }
		public string Prototype { get; set; }

		public RestrictionEnzyme(string name, string seq, int uppercut, int lowercut) : this(name, seq, uppercut, lowercut, "")
		{ }

		public RestrictionEnzyme(string name, string seq, int uppercut, int lowercut, string prototype)
		{
			Name = name;
			Sequence = new ShortDnaSequence(seq);
			UpperCutOffset = uppercut;
			LowerCutOffset = lowercut;
			Prototype = prototype;
		}

		public DnaSequence[] Cut(DnaSequence seq)
		{
			List<long> cuts = new List<long>();

			long start = 0;
			while(true)
			{
				long c;
				if(!seq.TryMatch(Sequence, start, out c)) break;
				cuts.Add(c + UpperCutOffset);
				start = c + 1;
			}

			start = 0;
			DnaSequence rc = new ShortDnaSequence(Sequence);
			rc.RevComp();
			while(true)
			{
				long c;
				if(!seq.TryMatch(rc, start, out c)) break;
				cuts.Add(c + Sequence.Count - LowerCutOffset);
				start = c + 1;
			}
			cuts.Sort();
			List<long> uniqueCuts = new List<long>();
			start = -1;
			for(int ix = 0; ix < cuts.Count; ix++)
			{
				if(cuts[ix] < 0) continue;
				if(cuts[ix] >= seq.Count) break;
				if(cuts[ix] != start) uniqueCuts.Add(cuts[ix]);
				start = cuts[ix];
			}
			DnaSequence[] result = new DnaSequence[uniqueCuts.Count + 1];
			start = 0;
			for(int ix = 0; ix < uniqueCuts.Count; ix++)
			{
				result[ix] = seq.SubSequence(start, uniqueCuts[ix] - start);
				start = uniqueCuts[ix];
			}
			if(uniqueCuts.Count != 0) result[result.Length - 1] = seq.SubSequence(uniqueCuts[uniqueCuts.Count - 1]);
			else result[0] = new ShortDnaSequence(seq);
			return result;
		}
	}
}
