using System;
using System.Windows;
using System.Windows.Input;
using Gigasoft.ProEssentials;
using Gigasoft.ProEssentials.Enums;
using System.Windows.Media;

namespace FinancialOhlcChart
{
    /// <summary>
    /// ProEssentials WPF Financial OHLC Chart — Trading Signal Generation
    ///
    /// Demonstrates a complete financial charting implementation using PegoWpf,
    /// the ProEssentials graph object for categorical (date-based) X-axis data.
    ///
    /// Chart features:
    ///   - OHLC candlestick chart with real stock CSV data (10 symbols)
    ///   - Bollinger Bands (Upper, SMA 20, Lower) — 20-day
    ///   - RSI — Relative Strength Index (10-day smoothed)
    ///   - Custom stochastic oscillator (30-day window, 15-day D-period)
    ///   - Buy/Sell signal annotations from stochastic turning point detection
    ///   - Table annotation hot spots — portfolio selector side panel
    ///   - Live OHLCV readout on mouse hover via DrawTable()
    ///   - Custom black tooltip with formatted OHLCV data
    ///   - Date/time axis with serial OADate handling
    ///   - Four synchronized multi-axes: Price 70%, Volume 10%, RSI 10%, Stoch 10%
    ///   - ZoomWindow overview panel, pinch/mouse wheel zoom, gesture pan
    ///   - Arrow-key cursor navigation after clicking a candlestick
    ///
    /// The Buy/Sell signal logic uses a custom stochastic oscillator with
    /// tuned parameters — the resulting signals could serve as input features
    /// to an AI/ML trading or decision support system.
    ///
    /// See StockPriceLoader.cs for all data loading and study calculations.
    ///
    /// User interactions:
    ///   - Click a stock symbol in the left panel to switch symbols
    ///   - Hover over candlesticks — live OHLCV data updates in the panel
    ///   - Left-click drag — draw a zoom box
    ///   - Mouse wheel — horizontal zoom
    ///   - Pinch gesture — zoom (touch screen / trackpad)
    ///   - Click a candlestick — enables arrow-key cursor navigation
    ///   - Right-click — full ProEssentials context menu
    ///   - Drag axis borders — resize axis proportions interactively
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Wire up all ProEssentials events in the constructor.
            // Important: always wire events before the control's Loaded event fires.
            // ProEssentials controls fire Loaded before the window's own Loaded event.

            // MouseMove: updates the OHLCV table annotation as the mouse moves
            Pego1.MouseMove += new MouseEventHandler(Pego1_MouseMove);

            // PeDataHotSpot: fires when user clicks a data point (candlestick)
            // Used here to engage the arrow-key cursor at the clicked point
            Pego1.PeDataHotSpot += new PegoWpf.DataHotSpotEventHandler(Pego1_PeDataHotSpot);

            // PeCursorMoved: fires when the arrow-key cursor moves to a new point
            // Used here to keep the OHLCV table annotation in sync with cursor position
            Pego1.PeCursorMoved += new PegoWpf.CursorMovedEventHandler(Pego1_PeCursorMoved);

            // PeTableAnnotation: fires when user clicks a table annotation hot spot
            // Used here to switch the active stock symbol when a symbol is clicked
            Pego1.PeTableAnnotation += new PegoWpf.TableAnnotationEventHandler(Pego1_PeTableAnnotation);

            // PeCustomTrackingDataText: fires when the tracking tooltip needs content
            // Used here to build a formatted multi-line OHLCV tooltip string
            Pego1.PeCustomTrackingDataText += new PegoWpf.CustomTrackingDataTextEventHandler(Pego1_PeCustomTrackingDataText);
        }

        // -----------------------------------------------------------------------
        // Pego1_Loaded — chart initialization
        //
        // Always initialize ProEssentials in the control's Loaded event.
        // Do NOT use the Window's Loaded event — it fires before the control
        // is fully initialized and IsChartInitialized will be false.
        // -----------------------------------------------------------------------
        void Pego1_Loaded(object sender, RoutedEventArgs e)
        {
            // Reset clears any prior state; Reinitialize prepares internal structures.
            // ModelessAutoClose is ideal when the control is never destroyed during
            // the application's lifetime — it auto-closes any open dialogs on reinit.
            Pego1.PeFunction.Reset();
            Pego1.PeUserInterface.Dialog.ModelessAutoClose = true;
            Pego1.PeFunction.Reinitialize();
            Pego1.PeConfigure.PrepareImages = true;

            // =======================================================================
            // Tracking cursor and tooltip configuration
            // =======================================================================

            // PromptTracking enables the live data cursor that follows the mouse.
            // ToolTip location renders the prompt as a floating tooltip near the cursor.
            Pego1.PeUserInterface.Cursor.PromptTracking          = true;
            Pego1.PeUserInterface.Cursor.PromptLocation          = CursorPromptLocation.ToolTip;
            Pego1.PeUserInterface.Cursor.PromptStyle             = CursorPromptStyle.XYValues;

            // TrackingTooltipMaxWidth caps the tooltip width in character units
            Pego1.PeUserInterface.Cursor.TrackingTooltipMaxWidth = 100;

            // TrackingCustomDataText = true routes tooltip content through the
            // PeCustomTrackingDataText event, giving full control over what is displayed
            Pego1.PeUserInterface.Cursor.TrackingCustomDataText  = true;

            // MouseCursorControl lets ProEssentials manage the cursor appearance
            // (e.g. crosshair inside the chart area, arrow outside)
            Pego1.PeUserInterface.Cursor.MouseCursorControl      = true;

            // Black background / white text tooltip — matches the dark chart theme
            Pego1.PeUserInterface.Cursor.TrackingTooltipBkColor   = Color.FromArgb(255, 0, 0, 0);
            Pego1.PeUserInterface.Cursor.TrackingTooltipTextColor = Color.FromArgb(255, 255, 255, 255);

            // ZoomWindow renders a small overview panel showing the full data range
            // with a highlighted rectangle indicating the current zoomed view position
            Pego1.PePlot.ZoomWindow.Show = true;

            // =======================================================================
            // Zoom, pan, and gesture configuration
            // =======================================================================

            // IsManipulationEnabled is the WPF property required for pinch-to-zoom
            // and two-finger pan gestures on touch screens and trackpads
            Pego1.IsManipulationEnabled = true;

            // HorizontalZoom scrolls the visible time window when the wheel is turned.
            // This is the most natural behavior for a financial chart.
            Pego1.PeUserInterface.Scrollbar.MouseWheelFunction = MouseWheelFunction.HorizontalZoom;

            // MouseDraggingX enables middle-mouse-button pan as well as being
            // required for touch pan gestures to function correctly
            Pego1.PeUserInterface.Scrollbar.MouseDraggingX           = true;
            Pego1.PeUserInterface.Scrollbar.MouseWheelZoomSmoothness = 4;  // 1=instant, higher=smoother
            Pego1.PeUserInterface.Scrollbar.PinchZoomSmoothness      = 2;

            // =======================================================================
            // Rendering quality
            // =======================================================================

            // BarGlassEffect adds a subtle 3D gloss to volume bars
            Pego1.PePlot.Option.BarGlassEffect = true;

            // PrepareImages pre-renders chart images in memory — eliminates flicker
            // during rapid updates such as MouseMove table annotation refreshes
            Pego1.PeConfigure.PrepareImages = true;

            // CacheBmp caches the rendered bitmap — only redraws when data changes.
            // Essential for smooth performance with complex multi-axis charts.
            Pego1.PeConfigure.CacheBmp = true;

            // Fixed font sizes prevent text from resizing when the window is resized,
            // keeping the panel layout stable and readable at any window size
            Pego1.PeFont.Fixed = true;

            // ScrollingScaleControl automatically adjusts the Y axis min/max
            // as the user pans or zooms horizontally — keeps candlesticks filling
            // the price axis without manual scale management
            Pego1.PeUserInterface.Scrollbar.ScrollingScaleControl = true;

            // =======================================================================
            // Portfolio selector — Table Annotation (Working Table 0)
            //
            // ProEssentials Table Annotations are floating panels that can be
            // positioned anywhere relative to the chart. Working = 0 selects the
            // first (left) table. Rows 0-9 are the stock symbol hot spots.
            // Rows 11-16 are updated dynamically on MouseMove with OHLCV data.
            // =======================================================================
            Pego1.PeAnnotation.Table.Working = 0;
            Pego1.PeAnnotation.Table.Rows    = 17;  // 10 symbols + divider + 6 OHLCV rows
            Pego1.PeAnnotation.Table.Columns = 1;

            Pego1.PeAnnotation.Table.Text[0, 0]  = " MSFT";
            Pego1.PeAnnotation.Table.Text[1, 0]  = " AAPL";
            Pego1.PeAnnotation.Table.Text[2, 0]  = " AMD";
            Pego1.PeAnnotation.Table.Text[3, 0]  = " AMZN";
            Pego1.PeAnnotation.Table.Text[4, 0]  = " CSCO";
            Pego1.PeAnnotation.Table.Text[5, 0]  = " META";
            Pego1.PeAnnotation.Table.Text[6, 0]  = " NFLX";
            Pego1.PeAnnotation.Table.Text[7, 0]  = " QCOM";
            Pego1.PeAnnotation.Table.Text[8, 0]  = " SBUX";
            Pego1.PeAnnotation.Table.Text[9, 0]  = " TSLA";
            Pego1.PeAnnotation.Table.Text[10, 0] = "-----------------";
            // Rows 11-16 are left empty here; MouseMove fills them with OHLCV data

            // HotSpot = true makes a table cell respond to mouse clicks.
            // Clicking fires the PeTableAnnotation event with the row/column index.
            for (int i = 0; i <= 9; i++)
                Pego1.PeAnnotation.Table.HotSpot[i, 0] = true;

            // Row 0 starts highlighted (MSFT is the initial symbol loaded)
            Pego1.PeAnnotation.Table.Color[0, 0]  = Color.FromArgb(255, 198, 0, 0);   // active = red
            for (int i = 1; i <= 13; i++)
                Pego1.PeAnnotation.Table.Color[i, 0] = Color.FromArgb(255, 142, 142, 142); // inactive = grey
            Pego1.PeAnnotation.Table.Color[14, 0] = Color.FromArgb(255, 0, 170, 0);    // O row = green
            Pego1.PeAnnotation.Table.Color[15, 0] = Color.FromArgb(255, 198, 0, 0);    // C row = red
            Pego1.PeAnnotation.Table.Color[16, 0] = Color.FromArgb(255, 190, 190, 190);// V row = light grey

            Pego1.PeAnnotation.Table.ColumnWidth[0] = 8;                           // character units
            Pego1.PeAnnotation.Table.Location        = GraphTALocation.LeftCenter; // left side, vertically centered
            Pego1.PeAnnotation.Table.Show            = true;
            Pego1.PeAnnotation.Table.Border          = TABorder.SingleLine;
            Pego1.PeAnnotation.Table.BackColor       = Color.FromArgb(255, 0, 0, 0);
            Pego1.PeAnnotation.Table.ForeColor       = Color.FromArgb(255, 250, 250, 250);
            Pego1.PeAnnotation.Table.TextSize        = 100; // relative units, 100 = normal size

            // =======================================================================
            // Load initial stock data (MSFT) and calculate all studies.
            // StockPriceLoader.LoadData() reads the CSV, calculates Bollinger Bands,
            // RSI, the custom stochastic, and places Buy/Sell signal annotations.
            // Date/time and multi-axis properties below must be set AFTER LoadData
            // because LoadData calls Reinitialize which resets the axis working index.
            // =======================================================================
            StockPriceLoader.LoadData("MSFT", Pego1);

            // =======================================================================
            // Date/time axis configuration
            //
            // ProEssentials supports serial date (OADate) mode for financial data.
            // DeltaX = -1 is a special code meaning "daily data with trading gaps"
            // — weekends and holidays are automatically skipped on the X axis.
            // DeltasPerDay = 1 means one data point per day.
            // DateTimeMode = true tells the axis to interpret X values as OADates.
            // =======================================================================
            Pego1.PeData.DeltasPerDay              = 1;
            Pego1.PeData.DeltaX                    = -1;   // -1 = daily data, skip non-trading days
            Pego1.PeData.DateTimeMode              = true;
            Pego1.PeGrid.Option.YearMonthDayPrompt = YearMonthDayPrompt.InsideTop;     // year/month label inside top of axis
            Pego1.PeGrid.Option.DayLabelType       = DayLabelType.ThreeCharacters;     // "Mon", "Tue" etc.
            Pego1.PeGrid.Option.MonthLabelType     = MonthLabelType.ThreeCharacters;   // "Jan", "Feb" etc.

            // =======================================================================
            // Multi-axis layout
            //
            // MultiAxesSubsets splits the total subsets (11) across 4 stacked axes.
            // The count in each slot is how many consecutive subsets go to that axis.
            // Subsets 0-6 → axis 0 (Price: High, Low, Open, Close, Boll Upper, SMA, Boll Lower)
            // Subset  7   → axis 1 (Volume)
            // Subset  8   → axis 2 (RSI)
            // Subsets 9-10→ axis 3 (Custom %K and %D stochastic)
            //
            // MultiAxesProportions controls the vertical height share of each axis.
            // =======================================================================
            Pego1.PeGrid.MultiAxesSubsets[0] = 7;   // Price axis: OHLC + Bollinger Upper/SMA/Lower
            Pego1.PeGrid.MultiAxesSubsets[1] = 1;   // Volume axis
            Pego1.PeGrid.MultiAxesSubsets[2] = 1;   // RSI axis
            Pego1.PeGrid.MultiAxesSubsets[3] = 2;   // Custom stochastic axis (%K and %D)

            Pego1.PeGrid.MultiAxesProportions[0] = 0.70F; // Price gets 70% of chart height
            Pego1.PeGrid.MultiAxesProportions[1] = 0.10F;
            Pego1.PeGrid.MultiAxesProportions[2] = 0.10F;
            Pego1.PeGrid.MultiAxesProportions[3] = 0.10F;

            // TwoDecimals precision applies globally to Y value display in tooltips
            // and the table annotation OHLCV readout
            Pego1.PeData.Precision = DataPrecision.TwoDecimals;

            // SeparateAxes draws each axis in its own horizontal band with its own
            // Y scale — this is the standard financial chart layout
            Pego1.PeGrid.Option.MultiAxisStyle = MultiAxisStyle.SeparateAxes;

            // MultiAxesSizing = true lets the user drag the axis divider lines to
            // interactively resize the proportion of each panel
            Pego1.PeUserInterface.Allow.MultiAxesSizing = true;

            // YAxisOnRight places the price scale on the right side, standard for
            // financial charts where the left side is occupied by the portfolio panel
            Pego1.PeGrid.Option.YAxisOnRight = true;

            // SpecificPlotModeColor enables multi-colored candlestick fills:
            // up days render in one color, down days in another
            Pego1.PePlot.Option.SpecificPlotModeColor = true;

            Pego1.PePlot.DataShadows              = DataShadows.None;
            Pego1.PeUserInterface.Allow.Zooming   = AllowZooming.HorzAndVert;
            Pego1.PeUserInterface.Allow.ZoomStyle = ZoomStyle.Ro2Not;

            // HotSpot.Data = true enables click detection on individual data points.
            // Size.Large makes the hot spot hit area larger, easier to click precisely.
            Pego1.PeUserInterface.HotSpot.Data = true;
            Pego1.PeUserInterface.HotSpot.Size = HotSpotSize.Large;

            Pego1.PeGrid.Option.ShowXAxis            = ShowAxis.GridNumbers;
            Pego1.PeString.MainTitle                 = "";  // stock symbol shown as watermark instead
            Pego1.PeString.SubTitle                  = "";
            Pego1.PeFont.FontSize                    = Gigasoft.ProEssentials.Enums.FontSize.Medium;
            Pego1.PePlot.PointSize                   = PointSize.Small;
            Pego1.PeUserInterface.Allow.Maximization = false;
            Pego1.PeGrid.LineControl                 = GridLineControl.Both;

            // SimpleLine/SimplePoint = true renders the legend using a simple line/dot
            // instead of full plot symbols — cleaner for a dense financial legend
            Pego1.PeLegend.SimpleLine  = true;
            Pego1.PeLegend.SimplePoint = true;

            // OneLineTopOfAxis places a compact single-line legend at the top of
            // each axis panel — ideal for the stacked multi-axis layout
            Pego1.PeLegend.Style                     = LegendStyle.OneLineTopOfAxis;
            Pego1.PeGrid.Configure.AutoMinMaxPadding = 1;

            // OhlcMinWidth = 4 sets the minimum candlestick body width in pixels.
            // As the chart zooms out and bars compress, this prevents them from
            // collapsing to 1px — they merge into a continuous filled bar instead,
            // which is more readable at compressed scales.
            Pego1.PePlot.Option.OhlcMinWidth = 4;

            // Hide certain menu items not relevant for this layout
            Pego1.PeUserInterface.Menu.GraphPlusTable       = MenuControl.Hide;
            Pego1.PeUserInterface.Menu.TableWhat            = MenuControl.Hide;
            Pego1.PeUserInterface.Menu.MultiAxisStyle       = MenuControl.Show;
            Pego1.PeUserInterface.Menu.LegendLocation       = MenuControl.Show;
            Pego1.PeUserInterface.Menu.ShowTableAnnotations = MenuControl.Show;
            Pego1.PeUserInterface.Menu.AnnotationControl    = true;
            Pego1.PeUserInterface.Dialog.PrintStyleControl  = PrintStyleControl.DefaultMonochrome;

            // =======================================================================
            // Per-axis plot methods
            //
            // WorkingAxis selects which axis subsequent plot/grid properties apply to.
            // Each axis has its own plot method, Y scale, and display options.
            // Always reset WorkingAxis to 0 when finished.
            // =======================================================================

            // Axis 0 — Price: SpecificPlotMode.BoxPlot renders OHLC candlesticks.
            // ComparisonSubsets = 3 tells BoxPlot which subsets are High/Low/Open/Close
            // (0=High, 1=Low, 2=Open, 3=Close — 3 is the count of comparison subsets
            // used to determine candle body boundaries)
            Pego1.PeGrid.WorkingAxis       = 0;
            Pego1.PePlot.Method            = GraphPlottingMethod.SpecificPlotMode;
            Pego1.PePlot.SpecificPlotMode  = SpecificPlotMode.BoxPlot;
            Pego1.PePlot.ComparisonSubsets = 3;
            Pego1.PeGrid.Option.ShowYAxis  = ShowAxis.GridNumbers;

            // Axis 1 — Volume bars
            Pego1.PeGrid.WorkingAxis      = 1;
            Pego1.PePlot.Method           = GraphPlottingMethod.Bar;
            Pego1.PeGrid.Option.ShowYAxis = ShowAxis.GridNumbers;

            // Axis 2 — RSI line
            Pego1.PeGrid.WorkingAxis      = 2;
            Pego1.PePlot.Method           = GraphPlottingMethod.Line;
            Pego1.PeGrid.Option.ShowYAxis = ShowAxis.GridNumbers;

            // Axis 3 — Custom stochastic area fill
            Pego1.PeGrid.WorkingAxis      = 3;
            Pego1.PePlot.Method           = GraphPlottingMethod.Area;
            Pego1.PeGrid.Option.ShowYAxis = ShowAxis.GridNumbers;

            // Always reset WorkingAxis to 0 after per-axis configuration
            Pego1.PeGrid.WorkingAxis = 0;

            // =======================================================================
            // Subset colors — 11 subsets total across all 4 axes
            // =======================================================================
            Pego1.PeColor.SubsetColors[0]  = Color.FromArgb(255, 255, 255, 255); // High (candlestick wick)
            Pego1.PeColor.SubsetColors[1]  = Color.FromArgb(255, 240, 3, 3);     // Low
            Pego1.PeColor.SubsetColors[2]  = Color.FromArgb(255, 240, 5, 5);     // Open
            Pego1.PeColor.SubsetColors[3]  = Color.FromArgb(255, 25, 198, 25);   // Close
            Pego1.PeColor.SubsetColors[4]  = Color.FromArgb(255, 126, 250, 200); // Bollinger Upper
            Pego1.PeColor.SubsetColors[5]  = Color.FromArgb(255, 212, 168, 0);   // SMA 20
            Pego1.PeColor.SubsetColors[6]  = Color.FromArgb(255, 200, 50, 50);   // Bollinger Lower
            Pego1.PeColor.SubsetColors[7]  = Color.FromArgb(255, 126, 250, 62);  // Volume
            Pego1.PeColor.SubsetColors[8]  = Color.FromArgb(255, 33, 200, 69);   // RSI
            Pego1.PeColor.SubsetColors[9]  = Color.FromArgb(255, 163, 160, 250); // Custom %K
            Pego1.PeColor.SubsetColors[10] = Color.FromArgb(155, 212, 216, 0);   // Custom %D

            // Subset line types for the legend — thin lines for OHLC/studies,
            // slightly thicker for the stochastic lines to distinguish them
            for (int i = 0; i <= 6; i++)
                Pego1.PeLegend.SubsetLineTypes[i] = LineType.ThinSolid;
            Pego1.PeLegend.SubsetLineTypes[7]  = LineType.MediumThinSolid;
            Pego1.PeLegend.SubsetLineTypes[8]  = LineType.MediumThinSolid;
            Pego1.PeLegend.SubsetLineTypes[9]  = LineType.MediumThinSolid;
            Pego1.PeLegend.SubsetLineTypes[10] = LineType.MediumThinSolid;

            // =======================================================================
            // Subset labels — shown in each axis legend
            // Custom %K/%D labels clarify these are NOT standard stochastic parameters
            // =======================================================================
            Pego1.PeString.SubsetLabels[0]  = "High";
            Pego1.PeString.SubsetLabels[1]  = "Low";
            Pego1.PeString.SubsetLabels[2]  = "Open";
            Pego1.PeString.SubsetLabels[3]  = "Close";
            Pego1.PeString.SubsetLabels[4]  = "Bollinger Upper";
            Pego1.PeString.SubsetLabels[5]  = "SMA 20";
            Pego1.PeString.SubsetLabels[6]  = "Bollinger Lower";
            Pego1.PeString.SubsetLabels[7]  = "Volume";
            Pego1.PeString.SubsetLabels[8]  = "Relative Strength Index - 10";
            Pego1.PeString.SubsetLabels[9]  = "Custom %K (30d)";  // tuned: 30-day window (standard is 14)
            Pego1.PeString.SubsetLabels[10] = "Custom %D (15d)";  // tuned: 15-day D-period (standard is 3)

            // =======================================================================
            // Visual styling — dark theme
            // =======================================================================
            Pego1.PePlot.Option.GradientBars      = 14;  // gradient intensity for volume bars
            Pego1.PeConfigure.TextShadows         = TextShadows.BoldText;
            Pego1.PeFont.MainTitle.Bold           = true;
            Pego1.PeFont.SubTitle.Bold            = true;
            Pego1.PeFont.Label.Bold               = true;
            Pego1.PePlot.Option.LineShadows       = true;
            Pego1.PeFont.FontSize                 = Gigasoft.ProEssentials.Enums.FontSize.Medium;
            Pego1.PePlot.DataShadows              = DataShadows.Shadows;

            // BitmapGradientMode = true enables GPU-accelerated gradient fills
            // QuickStyle.DarkNoBorder applies the dark color theme preset
            Pego1.PeColor.BitmapGradientMode      = true;
            Pego1.PeColor.QuickStyle              = QuickStyle.DarkNoBorder;

            Pego1.PeUserInterface.Menu.AnnotationControl  = true;
            Pego1.PeUserInterface.Menu.ShowAnnotationText = MenuControl.Show;

            Pego1.PePlot.Option.PointGradientStyle  = PlotGradientStyle.VerticalAscentInverse;
            Pego1.PeColor.PointBorderColor          = Color.FromArgb(100, 0, 0, 0);
            Pego1.PePlot.Option.LineSymbolThickness = 3;
            Pego1.PePlot.Option.AreaBorder          = 0;
            Pego1.PePlot.Option.SolidLineOverArea   = 1; // draw a solid line on top of area fill

            // ImageAdjust values add padding inside the chart image boundary,
            // giving annotations and axis labels more breathing room
            Pego1.PeConfigure.ImageAdjustTop   = 75;
            Pego1.PeConfigure.ImageAdjustLeft  = 75;
            Pego1.PeConfigure.ImageAdjustRight = 75;

            // =======================================================================
            // Export defaults — pre-configure the built-in export dialog
            // Right-click the chart and select Export to access these settings
            // =======================================================================
            Pego1.PeSpecial.DpiX = 600;
            Pego1.PeSpecial.DpiY = 600;
            Pego1.PeUserInterface.Dialog.ExportSizeDef  = ExportSizeDef.NoSizeOrPixel;
            Pego1.PeUserInterface.Dialog.ExportTypeDef  = ExportTypeDef.Png;
            Pego1.PeUserInterface.Dialog.ExportDestDef  = ExportDestDef.Clipboard;
            Pego1.PeUserInterface.Dialog.ExportUnitXDef = "1280";
            Pego1.PeUserInterface.Dialog.ExportUnitYDef = "768";
            Pego1.PeUserInterface.Dialog.ExportImageDpi = 300;

            // Direct2D render engine with anti-aliasing for crisp lines and text
            Pego1.PeConfigure.RenderEngine      = RenderEngine.Direct2D;
            Pego1.PeConfigure.AntiAliasText     = true;
            Pego1.PeConfigure.AntiAliasGraphics = true;

            // ReinitializeResetImage applies all property changes and renders the chart.
            // Always call this as the final step after setting all properties.
            Pego1.PeFunction.ReinitializeResetImage();
            Pego1.Invalidate();
        }

        // -----------------------------------------------------------------------
        // Pego1_MouseMove — live OHLCV readout in the table annotation
        //
        // On every mouse move, converts the pixel coordinate to a data point index
        // using ConvPixelToGraph(), reads OHLCV values at that index, and updates
        // the lower rows of the table annotation using DrawTable().
        //
        // DrawTable(0) is a lightweight partial redraw that updates only the
        // table annotation without triggering a full chart redraw — this is what
        // makes the live OHLCV readout fast and flicker-free.
        // -----------------------------------------------------------------------
        void Pego1_MouseMove(object sender, MouseEventArgs e)
        {
            Double fX = 0, fY = 0;
            float fHigh, fLow, fOpen, fClose, fVolume;
            int t, nA, nX, nY;
            String szDate, szFmt;

            // LastMouseMovePoint returns the last recorded mouse position in
            // control-relative pixel coordinates as a ProEssentials Point struct
            Gigasoft.ProEssentials.Structs.Point pt = Pego1.PeUserInterface.Cursor.LastMouseMovePoint;

            // ConvPixelToGraph converts pixel (nX, nY) to chart data coordinates (fX, fY).
            // nA = 0: operate on axis 0 (price axis). The function returns the fractional
            // point index in fX — e.g. 42.7 means between point 42 and point 43.
            nA = 0;
            nX = pt.X;
            nY = pt.Y;
            Pego1.PeFunction.ConvPixelToGraph(ref nA, ref nX, ref nY, ref fX, ref fY, false, false, false);

            // Round to nearest integer point index
            fY = Math.Abs(fX - Convert.ToInt32(fX));
            if (fY > 0.5)
                nX = Convert.ToInt32(fX);
            else
                nX = Convert.ToInt32(fX) - 1;

            // Clear OHLCV rows if mouse is outside the data range
            if (fX < 0 || fX > 1258)
            {
                Pego1.PeAnnotation.Table.Text[11, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[12, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[13, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[14, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[15, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[16, 0] = "        ";
                Pego1.PeFunction.DrawTable(0);
                return;
            }

            nX = Convert.ToInt32(nX - 1.0F);

            // Read OHLCV from the data arrays at the resolved point index
            fHigh   = Pego1.PeData.Y[0, nX]; // subset 0 = High
            fLow    = Pego1.PeData.Y[1, nX]; // subset 1 = Low
            fOpen   = Pego1.PeData.Y[2, nX]; // subset 2 = Open
            fClose  = Pego1.PeData.Y[3, nX]; // subset 3 = Close
            fVolume = Pego1.PeData.Y[7, nX]; // subset 7 = Volume
            szDate  = Pego1.PeData.PointLabels[nX];

            // Build a format string matching the chart's current decimal precision
            nX    = Convert.ToInt32(Pego1.PeData.Precision);
            szFmt = "###.";
            for (t = 0; t <= (nX - 1); t++)
                szFmt = szFmt + "0";

            // Update rows 11-16 with live OHLCV data.
            // Working = 0 ensures we are targeting the portfolio selector table.
            Pego1.PeAnnotation.Table.Working     = 0;
            Pego1.PeAnnotation.Table.Text[11, 0] = szDate;
            Pego1.PeAnnotation.Table.Text[12, 0] = "H:" + String.Format("{0:" + szFmt + "}", fHigh);
            Pego1.PeAnnotation.Table.Text[13, 0] = "L:" + String.Format("{0:" + szFmt + "}", fLow);
            Pego1.PeAnnotation.Table.Text[14, 0] = "O:" + String.Format("{0:" + szFmt + "}", fOpen);
            Pego1.PeAnnotation.Table.Text[15, 0] = "C:" + String.Format("{0:" + szFmt + "}", fClose);
            Pego1.PeAnnotation.Table.Text[16, 0] = "V:" + String.Format("{0:#############}", fVolume);

            // DrawTable(0) redraws only table annotation 0 — fast partial update,
            // no full chart redraw triggered
            Pego1.PeFunction.DrawTable(0);
        }

        // -----------------------------------------------------------------------
        // Pego1_PeTableAnnotation — stock symbol click handler
        //
        // Fires when a hot spot table cell is clicked. WorkingTable = 0 confirms
        // it is the portfolio selector table (not another table annotation).
        // Reads the symbol text, reloads data, and refreshes the chart.
        // -----------------------------------------------------------------------
        void Pego1_PeTableAnnotation(object sender, Gigasoft.ProEssentials.EventArg.TableAnnotationEventArgs e)
        {
            if (e.WorkingTable == 0)
            {
                // Clear any active cursor and zoom state before switching symbols
                Pego1.PeUserInterface.Cursor.Mode             = CursorMode.NoCursor;
                Pego1.PeGrid.Zoom.Mode                        = false;
                Pego1.PeUserInterface.Scrollbar.PointsToGraph = 0; // 0 = show all points

                // Highlight the clicked symbol in yellow, reset all others to grey
                for (int i = 0; i <= 9; i++)
                    Pego1.PeAnnotation.Table.Color[i, 0] = Color.FromArgb(255, 142, 142, 142);
                Pego1.PeAnnotation.Table.Color[e.RowIndex, e.ColumnIndex] = Color.FromArgb(255, 255, 0, 0);

                // Read and trim the symbol text from the clicked cell
                String szSym = Pego1.PeAnnotation.Table.Text[e.RowIndex, e.ColumnIndex].Trim();

                // Reload all data and recalculate all studies for the new symbol
                StockPriceLoader.LoadData(szSym, Pego1);

                // ReinitializeResetImage applies all changes and triggers a full redraw
                Pego1.PeFunction.ReinitializeResetImage();
                Pego1.Invalidate();
            }
        }

        // -----------------------------------------------------------------------
        // Pego1_PeCustomTrackingDataText — custom tooltip content
        //
        // Fires whenever the tracking tooltip needs to render its text content.
        // TrackingPromptTrigger tells us whether the trigger was a MouseMove or
        // a CursorMove (arrow key) — the point index is resolved differently
        // for each case.
        //
        // Setting e.TrackingText replaces the default coordinate display with
        // custom formatted OHLCV content inside the black tooltip.
        // -----------------------------------------------------------------------
        void Pego1_PeCustomTrackingDataText(object sender, Gigasoft.ProEssentials.EventArg.CustomTrackingDataTextEventArgs e)
        {
            double fX = 0.0F, fY = 0.0;
            float fHigh, fLow, fOpen, fClose, fVolume;
            int t, nA, nX, nY;
            String szDate, szFmt;

            System.Windows.Point pt = Pego1.PeUserInterface.Cursor.LastMouseMove;

            nA = 0;
            nX = (int)pt.X;
            nY = (int)pt.Y;
            Pego1.PeFunction.ConvPixelToGraph(ref nA, ref nX, ref nY, ref fX, ref fY, false, false, false);

            fY = Math.Abs(fX - Convert.ToInt32(fX));
            if (fY > 0.5)
                nX = Convert.ToInt32(fX);
            else
                nX = Convert.ToInt32(fX) - 1;

            if (fX < 0 || fX > 1258)
            {
                // Mouse is outside data range — clear OHLCV rows and exit
                Pego1.PeAnnotation.Table.Text[11, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[12, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[13, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[14, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[15, 0] = "        ";
                Pego1.PeAnnotation.Table.Text[16, 0] = "        ";
                Pego1.PeFunction.DrawTable(0);
                return;
            }

            nX = Convert.ToInt32(nX - 1.0F);

            // If trigger was not MouseMove (i.e. arrow-key cursor), use the
            // cursor's current point index instead of the mouse position
            if (Pego1.PeUserInterface.Cursor.TrackingPromptTrigger != TrackingTrigger.MouseMove)
                nX = Pego1.PeUserInterface.Cursor.Point;

            fHigh   = Pego1.PeData.Y[0, nX];
            fLow    = Pego1.PeData.Y[1, nX];
            fOpen   = Pego1.PeData.Y[2, nX];
            fClose  = Pego1.PeData.Y[3, nX];
            fVolume = Pego1.PeData.Y[7, nX];
            szDate  = Pego1.PeData.PointLabels[nX];

            nX    = Convert.ToInt32(Pego1.PeData.Precision);
            szFmt = "###.";
            for (t = 0; t <= (nX - 1); t++)
                szFmt = szFmt + "0";

            // Format the date using en-US culture to guarantee MM/dd/yyyy parsing
            System.Globalization.CultureInfo MyCultureInfo = new System.Globalization.CultureInfo("en-US");
            DateTime MyDateTime = DateTime.Parse(szDate, MyCultureInfo);

            // Build the multi-line tooltip string — \n creates line breaks inside
            // the ProEssentials tooltip
            String szPrompt  = MyDateTime.ToString("MM/dd/yyyy") + "    \n";
            szPrompt += "H:" + String.Format("{0:" + szFmt + "}", fHigh)   + "\n";
            szPrompt += "L:" + String.Format("{0:" + szFmt + "}", fLow)    + "\n";
            szPrompt += "O:" + String.Format("{0:" + szFmt + "}", fOpen)   + "\n";
            szPrompt += "C:" + String.Format("{0:" + szFmt + "}", fClose)  + "\n";
            szPrompt += "V:" + String.Format("{0:#############}", fVolume);

            // Assigning e.TrackingText replaces the default tooltip content
            e.TrackingText = szPrompt;
        }

        // -----------------------------------------------------------------------
        // Pego1_PeCursorMoved — arrow-key cursor navigation
        //
        // Fires each time the keyboard cursor moves to a new data point.
        // Click a candlestick first (PeDataHotSpot) to activate the cursor,
        // then use arrow keys to step through the data point by point.
        // Updates the OHLCV table annotation to match the cursor position.
        // -----------------------------------------------------------------------
        void Pego1_PeCursorMoved(object sender, EventArgs e)
        {
            int nX, t;
            float fHigh, fLow, fOpen, fClose, fVolume;
            String szDate, szFmt;

            // Cursor.Point is the current point index after the move
            nX = Pego1.PeUserInterface.Cursor.Point;

            fHigh   = Pego1.PeData.Y[0, nX];
            fLow    = Pego1.PeData.Y[1, nX];
            fOpen   = Pego1.PeData.Y[2, nX];
            fClose  = Pego1.PeData.Y[3, nX];
            fVolume = Pego1.PeData.Y[7, nX];
            szDate  = Pego1.PeData.PointLabels[nX];

            nX    = Convert.ToInt32(Pego1.PeData.Precision);
            szFmt = "###.";
            for (t = 0; t <= (nX - 1); t++)
                szFmt = szFmt + "0";

            Pego1.PeAnnotation.Table.Working     = 0;
            Pego1.PeAnnotation.Table.Text[11, 0] = szDate;
            Pego1.PeAnnotation.Table.Text[12, 0] = "H:" + String.Format("{0:" + szFmt + "}", fHigh);
            Pego1.PeAnnotation.Table.Text[13, 0] = "L:" + String.Format("{0:" + szFmt + "}", fLow);
            Pego1.PeAnnotation.Table.Text[14, 0] = "O:" + String.Format("{0:" + szFmt + "}", fOpen);
            Pego1.PeAnnotation.Table.Text[15, 0] = "C:" + String.Format("{0:" + szFmt + "}", fClose);
            Pego1.PeAnnotation.Table.Text[16, 0] = "V:" + String.Format("{0:#############}", fVolume);

            Pego1.PeFunction.DrawTable(0);
        }

        // -----------------------------------------------------------------------
        // Pego1_PeDataHotSpot — candlestick click handler
        //
        // Fires when the user clicks a data hot spot (a candlestick).
        // Sets the cursor mode to Point and positions it at the clicked point.
        // After this, arrow keys navigate the cursor through the data.
        // -----------------------------------------------------------------------
        void Pego1_PeDataHotSpot(object sender, Gigasoft.ProEssentials.EventArg.DataHotSpotEventArgs e)
        {
            Pego1.PeUserInterface.Cursor.Mode  = CursorMode.Point;   // vertical bar cursor
            Pego1.PeUserInterface.Cursor.Point = e.PointIndex;       // jump to clicked point
        }

        // -----------------------------------------------------------------------
        // Window_Closing
        // -----------------------------------------------------------------------
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }
    }
}
