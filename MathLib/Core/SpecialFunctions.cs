using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class SpecialFunctions
	{
		#region Range checks
		public static bool IsInteger(double x)
		{
			return Math.Truncate(x) == x;
		}

		public static bool IsInRange(double x, double min, double max)
		{
			return x >= min && x <= max;
		}

		public static bool IsPositiveInteger(double x)
		{
			return Math.Truncate(x) == x && x >= 0;

		}
		#endregion

		#region Combinatorial
		public static double BinomialCoefficient(int n, int k)
		{
			return Math.Floor(0.5d + Math.Exp(LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k)));
		}
		/// <summary>
		/// Returns the number of unordered combinations of k elements chosen from n. Equivalent
		/// to BinomialCoefficient(n, k)
		/// </summary>
		/// <param name="n"></param>
		/// <param name="k"></param>
		/// <returns></returns>
		public static double Combinations(int n, int k)
		{
			return Math.Floor(0.5d + Math.Exp(LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k)));
		}

		/// <summary>
		/// Returns the number of ordered combinations of k elements chosen from n.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="k"></param>
		/// <returns></returns>
		public static double Permutations(int n, int k)
		{
			return Math.Floor(0.5d + Math.Exp(LogFactorial(n) - LogFactorial(n - k)));
		}

		#endregion

		#region Error function
		public static double ErrorFunction(double x)
		{
			if(x < 0) return -RegularizedIncompleteGammaP(0.5, x * x);
			return RegularizedIncompleteGammaP(0.5, x * x);
		}

		public static double ErrorFunctionComplement(double x)
		{
			if(x < 0) return 1 + RegularizedIncompleteGammaP(0.5, x * x);
			return RegularizedIncompleteGammaQ(0.5, x * x);
		}
		#endregion

		#region Beta and related
		public static double Beta(double z, double w)
		{
			if(z == 0.0 || w == 0.0) return double.PositiveInfinity; // in fact ComplexInfinity (i.e. undetermined phase)

			return Math.Exp(LogGamma(z) + LogGamma(w) - LogGamma(z + w));
		}

		public static double LogBeta(double z, double w)
		{
			return LogGamma(z) + LogGamma(w) - LogGamma(z + w);
		}

		/// <summary>
		/// Compute the incomplete beta function Bx(a,B) using a continued fraction algorithm.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double IncompleteBeta(double a, double b, double x)
		{
			// Check arguments
			if(x < 0.0 || x > 1.0) throw new ArgumentOutOfRangeException("IncompleteBeta: x must be between 0 and 1.");
			
			// Special cases
			if(x == 0.0) return 0.0;
			if(x == 1.0) return 1.0;

			// Compute the prefix to the continued fraction
			double prefix;
			if(x == 0.0 || x == 1.0) prefix = 0.0;
			else
				prefix = Math.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b) + a * Math.Log(x) + b * Math.Log(1.0 - x));
			if(x < (a + 1.0) / (a + b + 2.0))
				return prefix * BetaContinuedFraction(a, b, x) / a;
			else
				return 1.0 - prefix * BetaContinuedFraction(b, a, 1.0 - x) / b;
		}

		private static double BetaContinuedFraction(double a, double b, double x)
		{
			const int maxIterations = 100;
			const double epsilon = 1.0e-10;
			const double FPMIN = 1.0e-30;

			int m, m2;
			double aa, c, d, del, h, qab, qam, qap;

			qab = a + b;
			qap = a + 1.0;
			qam = a - 1.0;
			c = 1.0;
			d = 1.0 - qab * x / qap;
			if(Math.Abs(d) < FPMIN) d = FPMIN;
			d = 1.0 / d;
			h = d;
			for(m = 1; m <= maxIterations; m++)
			{
				m2 = 2 * m;
				aa = m * (b - m) * x / ((qam + m2) * (a + m2));
				d = 1.0 + aa * d;
				if(Math.Abs(d) < FPMIN) d = FPMIN;
				c = 1.0 + aa / c;
				if(Math.Abs(c) < FPMIN) c = FPMIN;
				d = 1.0 / d;
				h *= d * c;
				aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
				d = 1.0 + aa * d;
				if(Math.Abs(d) < FPMIN) d = FPMIN;
				c = 1.0 + aa / c;
				if(Math.Abs(c) < FPMIN) c = FPMIN;
				d = 1.0 / d;
				del = d * c;
				h *= del;
				if(Math.Abs(del - 1.0) < epsilon) break;
			}
			if(m > maxIterations) throw new ArgumentOutOfRangeException("BetaContinuedFraction did not converge");
			return h;
		}
		#endregion

		#region Gamma and related

		public static double LogGamma(double xx)
		{
			double x, y, tmp, ser;
			double[] cof = new Double[6];
			cof[0] = 76.18009172947146;
			cof[1] = -86.50532032941677;
			cof[2] = 24.01409824083091;
			cof[3] = -1.231739572450155;
			cof[4] = 0.1208650973866179e-2;
			cof[5] = -0.5395239384953e-5;
			int j;
			y = x = xx;
			tmp = x + 5.5;
			tmp -= (x + 0.5) * Math.Log(tmp);
			ser = 1.000000000190015;
			for(j = 0; j <= 5; j++) ser += cof[j] / ++y;
			return -tmp + Math.Log(2.5066282746310005 * ser / x);
		}

		/// <summary>
		/// Return Math.Exp(LogGamma(x))
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double Gamma(double x)
		{
			return Math.Exp(LogGamma(x));			
		}

		/// <summary>
		/// Returns the regularized incomplete gamma function P(a,x)
		/// </summary>
		/// <param name="a"></param>
		/// <param name="x"></param>
		/// <returns></returns>
		public static double RegularizedIncompleteGammaP(double a, double x)
		{
			if(x < 0.0 || a <= 0.0) throw new ArgumentOutOfRangeException();
			if(x < (a + 1.0))
			{
				return IncompleteGammaSeriesExpansion(a, x);
			}
			else
			{
				return 1.0 - IncompleteGammaContinuedFraction(a, x);
			}
		}

		public static double RegularizedIncompleteGammaQ(double a, double x)
		{
			if(x < 0.0 || a <= 0.0) throw new ArgumentOutOfRangeException();
			if(x < (a + 1.0))
			{
				return 1.0 - IncompleteGammaSeriesExpansion(a, x);
			}
			else
			{
				return IncompleteGammaContinuedFraction(a, x);
			}
		}

		private static double IncompleteGammaSeriesExpansion(double a, double x)
		{
			const int maxIterations = 100;
			const double epsilon = 1.0e-15;

			int n;
			double sum, del, ap;

			if(x < 0.0) throw new ArgumentOutOfRangeException("IncompleteGammaSeriesExpansion: x must be positive.");
			if(x == 0.0)
			{
				return 0.0;
			}
			else
			{
				ap = a;
				del = sum = 1.0 / a;
				for(n = 1; n <= maxIterations; n++)
				{
					++ap;
					del *= x / ap;
					sum += del;
					if(Math.Abs(del) < Math.Abs(sum) * epsilon)
					{
						return sum * Math.Exp(-x + a * Math.Log(x) - LogGamma(a));
					}
				}
				throw new ArgumentOutOfRangeException("IncompleteGammaSeriesExpansion did not converge");
			}
		}

		private static double IncompleteGammaContinuedFraction(double a, double x)
		{
			const int maxIterations = 100;
			const double epsilon = 1.0e-10;
			const double FPMIN = 1.0e-30;

	        int i;
	        double an,b,c,d,del,h;

	        b=x+1.0-a;
	        c=1.0/FPMIN;
	        d=1.0/b;
	        h=d;
	        for (i=1;i<=maxIterations;i++) 
			{
		        an = -i*(i-a);
		        b += 2.0;
		        d=an*d+b;
		        if (Math.Abs(d) < FPMIN) d=FPMIN;
		        c=b+an/c;
		        if (Math.Abs(c) < FPMIN) c=FPMIN;
		        d=1.0/d;
		        del=d*c;
		        h *= del;
		        if (Math.Abs(del-1.0) < epsilon) break;
	        }
			if(i > maxIterations) throw new ArgumentOutOfRangeException("GammaContinuedFraction did not converge");
 
	        return Math.Exp(-x+a*Math.Log(x)-LogGamma(a))*h;
		}
		#endregion

		#region Factorial and related
		private static double[] precomputedFactorials = new double[] 
			{
			1, 1, 2, 6, 24, 120, 720, 5040, 40320, 362880, 3628800, 39916800, 479001600, 
			6227020800, 87178291200, 1307674368000, 20922789888000, 355687428096000, 
			6402373705728000, 121645100408832000, 2432902008176640000, 
			51090942171709440000d, 1124000727777607680000d, 25852016738884976640000d, 
			620448401733239439360000d, 15511210043330985984000000d, 
			403291461126605635584000000d, 10888869450418352160768000000d, 
			304888344611713860501504000000d, 8841761993739701954543616000000d, 
			265252859812191058636308480000000d, 8222838654177922817725562880000000d, 263130836933693530167218012160000000d, 
			8683317618811886495518194401280000000d, 
			295232799039604140847618609643520000000d, 
			10333147966386144929666651337523200000000d, 
			371993326789901217467999448150835200000000d, 
			13763753091226345046315979581580902400000000d, 
			523022617466601111760007224100074291200000000d, 
			20397882081197443358640281739902897356800000000d, 
			815915283247897734345611269596115894272000000000d
			};

		/// <summary>
		/// Returns the factorial using precomputed numbers up to n=40 and using Gamma(n + 1) above 40.
		/// Precomputed factorials were obtained using Mathematica 5.1.
		/// </summary>
		/// <param name="n"></param>
		/// <returns></returns>
		public static double Factorial(int n)
		{
			if(n < 0) throw new ArgumentOutOfRangeException();
			if(n >= precomputedFactorials.Length) return Gamma(n + 1.0);
			return precomputedFactorials[n];
		}

		private static double[] precomputedLogFactorials = new double[]
			{0, 0, 0.6931471805599453094172321214581765680755, 
			1.791759469228055000812477358380702272723, 
			3.178053830347945619646941601297055408874,
			4.787491742782045994247700934523243048400, 
			6.579251212010100995060178292903945321123, 
			8.525161361065414300165531036347125050760, 
			10.60460290274525022841722740072165475499, 
			12.80182748008146961120771787456670616428, 
			15.10441257307551529522570932925107037188, 
			17.50230784587388583928765290721619967170, 
			19.98721449566188614951736238705507851250, 
			22.55216385312342288557084982862039711731, 
			25.19122118273868150009343469352175341502, 
			27.89927138384089156608943926367046675919, 
			30.67186010608067280375836774950317303150, 
			33.50507345013688888400790236737629956708, 
			36.39544520803305357621562496267952754445, 
			39.33988418719949403622465239456738108169, 
			42.33561646075348502965987597070992185737, 
			45.38013889847690802616047395107562729165, 
			48.47118135183522387963964965049893315955, 
			51.60667556776437357044640248230912927799, 
			54.78472939811231919009334408360618468687, 
			58.00360522298051993929486275005855996592, 
			61.26170176100200198476558231308205513880, 
			64.55753862700633105895131802384963225274, 
			67.88974313718153498289113501020916511853, 
			71.25703896716800901007440704257107672402, 
			74.65823634883016438548764373417796663627, 
			78.09222355331531063141680805872032384672, 
			81.55795945611503717850296866601120668710, 
			85.05446701758151741396015748089886169157, 
			88.58082754219767880362692422023016479523, 
			92.13617560368709248333303629689953216439, 
			95.71969454214320248495799101366093670984, 
			99.33061245478742692932608668469238387374, 
			102.9681986145138126987523462380384139791, 
			106.6317602606434591262010789165262582885, 
			110.3206397147573954290535346141269756323, 
			114.0342117814617032329202979871643832206, 
			117.7718813997450715388381280889882652230, 
			121.5330815154386339623109706023341122586, 
			125.3172711493568951252073784232155946945, 
			129.1239336391272148825986282302868337433, 
			132.9525750356163098828226131835552064299, 
			136.8027226373263684696435638533273801388, 
			140.6739236482342593987077375760826121157, 
			144.5657439463448860089184430629689715750, 
			148.4777669517730320675371938508795234221, 
			152.4095925844973578391819737056751756623, 
			156.3608363030787851940699253901568474033, 
			160.3311282166309070282143945291859051737, 
			164.3201122631951814118173623614116588557, 
			168.3274454484276523304800652726029757950, 
			172.3527971391628015638371143804206852289, 
			176.3958484069973517152413870492310644708, 
			180.4562914175437710518418912030511526443, 
			184.5338288614494905024579415767708502684, 
			188.6281734236715911872884103898359167487, 
			192.7390472878449024360397994932615314951, 
			196.8661816728899939913861959392620652736, 
			201.0093163992815266792820391565502964125, 
			205.1681994826411985357854318852993558210, 
			209.3425867525368356464396786600908620653, 
			213.5322414945632611913140995964366936378, 
			217.7369341139542272509841715928004163884, 
			221.9564418191303339500681704535898960601, 
			226.1905483237275933322701685223226178832, 
			230.4390435657769523213935127204501618205, 
			234.7017234428182677427229672529631959172, 
			238.9783895618343230537651540911827770308, 
			243.2688490029827141828572629486213196017, 
			247.5729140961868839366425907411109433336, 
			251.8904022097231943772393546444858443173, 
			256.2211355500095254560828463192900509907, 
			260.5649409718632093052501426406983600202, 
			264.9216497985528010421161074406443808977, 
			269.2910976510198225362890529821257918199, 
			273.6731242856937041485587408011846857317, 
			278.0675734403661429141397217488747885503, 
			282.4742926876303960274237172433703727067, 
			286.8931332954269939508991894666617431598, 
			291.3239500942703075662342516899438017302, 
			295.7666013507606240210845456410431159053, 
			300.2209486470141317539746202758471395090, 
			304.6868567656687154725531375451315768191, 
			309.1641935801469219448667774874712358232, 
			313.6528299498790617831845930281410850426, 
			318.1526396202093268499930749566705006595, 
			322.6634991267261768911519151416789989939, 
			327.1852877037752172007931322164055482485, 
			331.7178871969284731381175417778704311636, 
			336.2611819791984770343557245691007814406, 
			340.8150588707990178689655113342148226173, 
			345.3794070622668541074469171784282311623, 
			349.9541180407702369295636388001321928762, 
			354.5390855194408088491915764084767289035, 
			359.1342053695753987760440104602869096126, 
			363.7393755555634901440799933696556380278};

		public static double LogFactorial(int n)
		{
			if(n < 0) throw new ArgumentOutOfRangeException();
			if(n >= precomputedLogFactorials.Length) return LogGamma(n + 1.0);
			return precomputedLogFactorials[n];
		}
		#endregion

	}
}
