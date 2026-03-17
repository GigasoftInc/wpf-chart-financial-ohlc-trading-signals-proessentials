using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows.Media;
using Gigasoft.ProEssentials;
using Gigasoft.ProEssentials.Enums;

namespace FinancialOhlcChart
{
    /// <summary>
    /// Loads stock price CSV data and calculates technical studies:
    ///   - Bollinger Bands (20-day SMA, Upper, Lower)
    ///   - RSI — Relative Strength Index (10-day)
    ///   - Custom Stochastic Oscillator (30-day window, 15-day D-period)
    ///   - Buy/Sell signal annotations from stochastic turning point detection
    ///
    /// The Buy/Sell signal logic detects direction reversals in the Slow %D
    /// stochastic using a 7-point lookahead and a -6.0 slope threshold with
    /// a 60.0 overbought floor for sell signals. These tuned parameters
    /// produce signals that could serve as input features to an AI/ML system.
    ///
    /// CSV format: Date, Close, Volume, Open, High, Low
    /// </summary>
    public class StockPriceLoader
    {
        public static void LoadData(string newName, PegoWpf Pego1)
        {
            string szFile = newName + ".csv";

            float[]  y1  = new float[1400];   // Open
            float[]  y2  = new float[1400];   // High
            float[]  y3  = new float[1400];   // Low
            float[]  y4  = new float[1400];   // Close
            float[]  y5  = new float[1400];   // Volume
            double[] X0  = new double[1400];  // Serial dates
            string[] Xs  = new string[1400];  // Date labels
            int c = 0, i, nCnt;

            // --- Load CSV ---
            StreamReader sr = null;
            int LineCount = 0;
            try
            {
                LineCount = File.ReadLines(szFile).Count();
                sr        = File.OpenText(szFile);
            }
            catch
            {
                System.Windows.MessageBox.Show(
                    $"Data file '{szFile}' not found.\n\nMake sure the CSV files are in the same folder as the executable.",
                    "File Not Found",
                    System.Windows.MessageBoxButton.OK);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            int iCnt = 0;
            if (sr != null)
            {
                sr.ReadLine(); // skip header

                string input;
                CultureInfo EnCulture = new CultureInfo("en-US");

                while ((input = sr.ReadLine()) != null && iCnt < LineCount - 1)
                {
                    iCnt++;
                    string[] TempArray = input.Split(',');

                    string sDate   = TempArray[0];
                    string sClose  = TempArray[1].Replace('$', ' ');
                    string sVolume = TempArray[2];
                    string sOpen   = TempArray[3].Replace('$', ' ');
                    string sHigh   = TempArray[4].Replace('$', ' ');
                    string sLow    = TempArray[5].Replace('$', ' ');

                    int idx = LineCount - iCnt - 1;
                    Xs[idx] = sDate;

                    DateTime dateValue = DateTime.Parse(sDate, EnCulture);
                    X0[idx] = dateValue.Date.ToOADate();

                    y1[idx] = Convert.ToSingle(sOpen,   EnCulture);
                    y2[idx] = Convert.ToSingle(sHigh,   EnCulture);
                    y3[idx] = Convert.ToSingle(sLow,    EnCulture);
                    y4[idx] = Convert.ToSingle(sClose,  EnCulture);
                    y5[idx] = Convert.ToSingle(sVolume, EnCulture);

                    c++;
                }
                sr.Close();
            }

            // --- Pass OHLCV data to chart ---
            nCnt = c;
            Pego1.PeData.StartTime   = X0[0];
            Pego1.PeData.Subsets     = 11;
            Pego1.PeData.Points      = LineCount - 1;
            Pego1.PeData.UsingXDataii = true;

            // Reset arrays
            Pego1.PeData.Y[0, -1]        = 0;
            Pego1.PeData.Xii[0, -1]      = 0;
            Pego1.PeData.PointLabels[-1]  = "0";

            for (i = 0; i <= nCnt - 1; i++)
            {
                Pego1.PeData.Y[0, i]   = y2[i]; // High
                Pego1.PeData.Y[1, i]   = y3[i]; // Low
                Pego1.PeData.Y[2, i]   = y1[i]; // Open
                Pego1.PeData.Y[3, i]   = y4[i]; // Close
                Pego1.PeData.Y[7, i]   = y5[i]; // Volume
                Pego1.PeData.Xii[0, i] = X0[i];
                Pego1.PeData.PointLabels[i] = Xs[i];
            }

            // ---------------------------------------------------------------
            // Bollinger Bands — 20-day SMA, Upper (+2σ), Lower (-2σ)
            // ---------------------------------------------------------------
            float[] pYD3 = new float[nCnt];
            for (int pnt = 0; pnt <= nCnt - 1; pnt++)
                pYD3[pnt] = Pego1.PeData.Y[3, pnt];

            double Days = 20;
            for (int pnt = 0; pnt <= nCnt - Days - 1; pnt++)
            {
                double Total = 0;
                for (i = pnt; i <= Days + pnt - 1; i++)
                    Total += pYD3[i];
                float sma = (float)(Total / Days);
                Pego1.PeData.Y[5, (int)(pnt + Days - 1)] = sma; // SMA

                // Upper Band
                double BBNum = 0;
                for (i = pnt; i <= Days + pnt - 1; i++)
                    BBNum += (pYD3[i] - sma) * (pYD3[i] - sma);
                Pego1.PeData.Y[4, (int)(pnt + Days - 1)] = (float)(sma + 2.0 * Math.Sqrt(BBNum / Days));

                // Lower Band
                BBNum = 0;
                for (i = pnt; i <= Days + pnt - 1; i++)
                    BBNum += (pYD3[i] - sma) * (pYD3[i] - sma);
                Pego1.PeData.Y[6, (int)(pnt + Days - 1)] = (float)(sma - 2.0 * Math.Sqrt(BBNum / Days));
            }

            // ---------------------------------------------------------------
            // RSI — Relative Strength Index (10-day)
            // ---------------------------------------------------------------
            Days = 10;
            double RSITotal = 0, RSITotal2 = 0;
            float[] pUpperArray = new float[nCnt];
            float[] pLowerArray = new float[nCnt];
            int LowerCount = 0, UpperCount = 0;

            for (i = 1; i <= Days; i++)
            {
                if ((pYD3[i] - pYD3[i - 1]) < 0)
                    pLowerArray[LowerCount++] = pYD3[i] - pYD3[i - 1];
                else
                    pUpperArray[UpperCount++] = pYD3[i] - pYD3[i - 1];
            }

            for (i = 0; i <= LowerCount - 1; i++) RSITotal  += pLowerArray[i];
            for (i = 0; i <= UpperCount - 1; i++) RSITotal2 += pUpperArray[i];

            float RS  = (float)((RSITotal2 / Days) / (Math.Abs(RSITotal) / Days));
            float RSI = 100.0F - (100.0F / (1.0F + RS));
            Pego1.PeData.Y[8, (int)(Days - 1)] = RSI;

            for (i = (int)Days; i <= nCnt - 1; i++)
            {
                if (pYD3[i] != 0 && pYD3[i - 1] != 0)
                {
                    RSITotal  = RSITotal  * (Days - 1);
                    RSITotal2 = RSITotal2 * (Days - 1);
                    if ((pYD3[i] - pYD3[i - 1]) < 0)
                        RSITotal  += pYD3[i] - pYD3[i - 1];
                    else
                        RSITotal2 += pYD3[i] - pYD3[i - 1];
                    RSITotal  /= Days;
                    RSITotal2 /= Days;
                    RS  = (float)(RSITotal2 / Math.Abs(RSITotal));
                    RSI = 100.0F - (100.0F / (1.0F + RS));
                    Pego1.PeData.Y[8, (int)(Days - 1 + i)] = RSI;
                }
            }

            // RSI axis line at 50
            Pego1.PeAnnotation.Line.YAxis[0]     = 50.0;
            Pego1.PeAnnotation.Line.YAxisAxis[0] = 2;
            Pego1.PeAnnotation.Line.YAxisType[0] = LineAnnotationType.MediumThinSolid;
            Pego1.PeAnnotation.Line.YAxisColor[0]= Color.FromArgb(255, 198, 0, 0);
            Pego1.PeAnnotation.Show              = true;

            Pego1.PeGrid.WorkingAxis                       = 2;
            Pego1.PeGrid.Configure.ManualScaleControlY     = ManualScaleControl.MinMax;
            Pego1.PeGrid.Configure.ManualMinY              = 0;
            Pego1.PeGrid.Configure.ManualMaxY              = 100;

            // ---------------------------------------------------------------
            // Custom Stochastic Oscillator (30-day window, 15-day D-period)
            // Tuned parameters produce signals suitable as AI/ML input features
            // ---------------------------------------------------------------
            Days = 30;
            int SwFac   = 1;
            int Dperiod = 15;
            float LowMin = 9999.9F, HighMax = 0.0F;

            float[] pSlowOC  = new float[nCnt + 1];
            float[] pKperiod = new float[nCnt + 1];
            float[] pHigh    = new float[nCnt + 1];
            float[] pLow     = new float[nCnt + 1];

            for (int pnt = 0; pnt <= nCnt - 1; pnt++)
            {
                pHigh[pnt] = Pego1.PeData.Y[0, pnt];
                pLow[pnt]  = Pego1.PeData.Y[1, pnt];
            }

            for (int pnt = 0; pnt <= nCnt - Days - 1; pnt++)
            {
                for (i = pnt; i <= Days + pnt - 1; i++)
                    if (pHigh[i] != 0 && pHigh[i] > HighMax) HighMax = pHigh[i];
                for (i = pnt; i <= Days + pnt - 1; i++)
                    if (pLow[i] != 0 && pLow[i] < LowMin) LowMin = pLow[i];

                if (pYD3[(int)(Days - 1 + pnt)] != 0)
                    pKperiod[pnt] = ((pYD3[(int)(Days - 1 + pnt)] - LowMin) / (HighMax - LowMin)) * 100.0F;

                LowMin  = 9999.9F;
                HighMax = 0.0F;
            }

            for (int pnt = 0; pnt <= nCnt - 1 - SwFac; pnt++)
            {
                float SlowK = 0;
                for (int q = pnt; q <= SwFac + pnt - 1; q++)
                    SlowK += pKperiod[q];
                pSlowOC[pnt] = SlowK / SwFac;
                Pego1.PeData.Y[9, (int)(Days + pnt + SwFac)] = pSlowOC[pnt]; // Fast %K
            }

            for (int pnt = 0; pnt <= nCnt - 1 - Dperiod; pnt++)
            {
                float PercentD = 0;
                for (int q = pnt; q <= Dperiod + pnt - 1; q++)
                    PercentD += pSlowOC[q];
                Pego1.PeData.Y[10, (int)(Days + pnt + Dperiod)] = PercentD / Dperiod; // Slow %D
            }

            Pego1.PeGrid.WorkingAxis                   = 3;
            Pego1.PeGrid.Configure.ManualScaleControlY = ManualScaleControl.MinMax;
            Pego1.PeGrid.Configure.ManualMinY          = 0;
            Pego1.PeGrid.Configure.ManualMaxY          = 100;
            Pego1.PeGrid.WorkingAxis                   = 0;

            // ---------------------------------------------------------------
            // Buy/Sell Signal Annotations
            // Detects turning points in Slow %D using 7-point lookahead.
            // Sell: slope < -6.0 AND stochastic > 60 (overbought)
            // Buy:  direction reverses upward from downtrend
            // These signals could serve as input features to an AI/ML system.
            // ---------------------------------------------------------------
            Pego1.PeAnnotation.Graph.X.Clear();
            Pego1.PeAnnotation.Graph.Y.Clear();
            Pego1.PeAnnotation.Graph.Type.Clear();
            Pego1.PeAnnotation.Graph.Color.Clear();
            Pego1.PeAnnotation.Graph.Text.Clear();

            int nGA = 0;

            // TextBoundingBox annotation type for labels
            Pego1.PeAnnotation.Graph.Type[nGA]  = (int)GraphAnnotationType.TextBoundingBox;
            Pego1.PeAnnotation.Graph.Color[nGA] = Color.FromArgb(255, 0, 0, 0);
            nGA++;

            int nSearchingTurning = -1;
            int nTurn = 0;

            for (int pnt = 1; pnt <= nCnt - 8; pnt++)
            {
                float d2   = Pego1.PeData.Y[10, pnt + 7];
                float d1   = Pego1.PeData.Y[10, pnt];
                float fDif = d2 - d1;

                if (nSearchingTurning == -1)
                {
                    if (fDif == 0) continue;
                    nSearchingTurning = fDif < 0 ? 1 : 2;
                    continue;
                }

                if (nSearchingTurning == 1 && fDif > 0)
                {
                    // Buy signal — stochastic turning upward
                    nSearchingTurning = 2;
                    nTurn++;
                    Pego1.PeAnnotation.Graph.X[nGA]     = pnt + 1;
                    Pego1.PeAnnotation.Graph.Y[nGA]     = Pego1.PeData.Y[1, pnt];
                    Pego1.PeAnnotation.Graph.Type[nGA]  = (int)GraphAnnotationType.Pointer;
                    Pego1.PeAnnotation.Graph.Color[nGA] = Color.FromArgb(255, 0, 160, 0);
                    Pego1.PeAnnotation.Graph.Text[nGA]  = "Buy:" + nTurn.ToString();
                    nGA++;
                    pnt += 7;
                    continue;
                }

                if (nSearchingTurning == 2 && fDif < -6.0F && Pego1.PeData.Y[10, pnt] > 60.0F)
                {
                    // Sell signal — steep downward slope from overbought territory
                    nSearchingTurning = 1;
                    nTurn++;
                    Pego1.PeAnnotation.Graph.X[nGA]     = pnt + 1;
                    Pego1.PeAnnotation.Graph.Y[nGA]     = Pego1.PeData.Y[1, pnt];
                    Pego1.PeAnnotation.Graph.Type[nGA]  = (int)GraphAnnotationType.Pointer;
                    Pego1.PeAnnotation.Graph.Color[nGA] = Color.FromArgb(255, 255, 0, 0);
                    Pego1.PeAnnotation.Graph.Text[nGA]  = "Sell:" + nTurn.ToString();
                    nGA++;
                    pnt += 7;
                    continue;
                }
            }

            Pego1.PeAnnotation.Show                     = true;
            Pego1.PeFont.GraphAnnotationTextSize         = 110;
            Pego1.PeAnnotation.Graph.MinSymbolSize       = MinimumPointSize.Large;
            Pego1.PeAnnotation.Graph.MaxSymbolSize       = MinimumPointSize.Large;
            Pego1.PeUserInterface.HotSpot.GraphAnnotation = AnnotationHotSpot.GraphOnly;
            Pego1.PePlot.ZoomWindow.ShowAnnotations      = false;

            // Watermark: stock symbol name overlay
            Pego1.PeAnnotation.Table.Working         = 5;
            Pego1.PeAnnotation.Table.Rows            = 1;
            Pego1.PeAnnotation.Table.Columns         = 1;
            Pego1.PeAnnotation.Table.Location        = GraphTALocation.OverlapInsideAxis0;
            Pego1.PeAnnotation.Table.AxisLocation    = GraphTAAxisLocation.TopLeft;
            Pego1.PeAnnotation.Table.Show            = true;
            Pego1.PeAnnotation.Table.Border          = TABorder.NoBorder;
            Pego1.PeAnnotation.Table.Text[0, 0]      = newName;
            Pego1.PeAnnotation.Table.ForeColor       = Color.FromArgb(50, 155, 155, 155);
            Pego1.PeAnnotation.Table.TextSize        = 4500;
            Pego1.PeAnnotation.Table.BackColor       = Color.FromArgb(2, 0, 0, 0);
            Pego1.PeAnnotation.Table.Working         = 0; // always reset
        }
    }
}
