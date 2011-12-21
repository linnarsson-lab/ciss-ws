<?php
require_once ('jpgraph.php');
require_once ('jpgraph_bar.php');
require_once('jpgraph_error.php');
require_once('jpgraph_log.php');
require_once ('jpgraph_line.php');

// $y2scale is e.g. "log", or "" if no y2 is needed.
// A $curve on the y2 axis has to have ["yaxis"] == "right"
// If $xtitle == "" no x axis title will be printed.
function plotLines($scale, $y2scale, $title, $curves, $xtitle, $ymin = NULL, $ymax = NULL) {
	$graph = new Graph(600,500,'auto');
	if (!is_null($ymin) && !is_null($ymax))
	    $graph->SetScale($scale, $ymin, $ymax);
	else
	    $graph->SetScale($scale);
	$theme_class=new UniversalTheme;
	$graph->SetTheme($theme_class);
	$graph->img->SetAntiAliasing(false);
	$graph->title->Set($title);
	$graph->SetBox(false);
	$graph->SetMargin(40,140,30,50);
	$graph->img->SetAntiAliasing();
	$graph->yaxis->HideLine(false);
	$graph->yaxis->HideTicks(false,false);
	$graph->xgrid->SetLineStyle("solid");
	if ($y2scale != "")
    {   
	    $graph->SetY2Scale($y2scale);
	    $graph->y2axis->Hide(false);
	    $graph->y2axis->HideLine(false);
	    $graph->y2axis->HideTicks(false,false);
		$graph->y2axis->SetColor("black");
	}
	foreach ($curves as $line) {
	    if (is_numeric($line["xvalues"][0]))
            $p = new LinePlot($line["yvalues"], $line["xvalues"]);
		else
		{
            $p = new LinePlot($line["yvalues"]);
			$graph->xaxis->SetTickLabels($line["xvalues"]);
		}
		$p->mark->SetType(MARK_DIAMOND);
		$p->mark->SetFillColor($line["color"]);
		if ($line["yaxis"] == "right")
		    $graph->AddY2($p);
		else
   	        $graph->Add($p);
		$p->SetColor($line["color"]);
		$p->SetLegend($line["legend"]);
	}
	$graph->legend->SetFrameWeight(1);
	if (strlen($xtitle) > 0) {
        $graph->xaxis->SetTitle($xtitle, 'middle');
		$graph->xaxis->title->Align('center');
	}
	$graph->legend->Pos(0.01,0.3,"right","middle");
    $graph->legend->SetLayout(LEGEND_VERT);
	return $graph;
}

function plotHistogram($title, $xtitle, $barmids, $counts) {
    $graph = new Graph(500,400,'auto');
    if (is_numeric($barmids[0]))
        $graph->SetScale("linlin");
    else
        $graph->SetScale("textlin");
    $theme_class=new UniversalTheme;
    $graph->SetTheme($theme_class);
    $graph->title->Set($title);
    $graph->SetBox(false);
    $graph->SetMargin(50,20,20,50);
    $graph->yaxis->HideLine(false);
    $graph->yaxis->HideTicks(false,false);
    $graph->xgrid->SetLineStyle("solid");
    $bplot = new BarPlot($counts, $barmids);
    $graph->Add($bplot);
    //$graph->SetTickDensity(TICKD_SPARSE);
    //$bplot->SetColor("white");
    $bplot->SetFillColor("darkgreen");
    if (is_numeric($barmids[0])) {
        $width = ($barmids[1] - $barmids[0]);
	    $bplot->SetWidth($width);
    }
    $graph->legend->SetFrameWeight(1);
    $graph->xaxis->HideTicks(false,false);
    if (strlen($xtitle) > 0) {
        $graph->xaxis->SetTitle($xtitle, 'middle');
        $graph->xaxis->title->Align('center');
    }
    return $graph;
}

function plotBarsByCategory($scaletypes, $title, $xtitle, $categories, $yvalues) {
    $width = 1024;
    #if (count($categories) > 6) $width += (count($categories) - 6) * 18;
    $graph = new Graph($width, 500, 'auto');
    $graph->SetScale($scaletypes);
    $theme_class=new UniversalTheme;
    $graph->SetTheme($theme_class);
    $graph->SetBox(false);
    $graph->SetMargin(100,20,50,50);
    $graph->ygrid->SetFill(false);
    $graph->xaxis->SetLabelMargin(5);
    $tickdist = intval(count($categories) / 25);
    for ($i = 0; $i < count($categories); $i++)
        if ($i != count($categories)-1 && ($i % $tickdist != 0 || $i > count($categories)-$tickdist))
             $categories[$i] = " ";
    $graph->xaxis->SetTickLabels($categories);
    $graph->yaxis->HideLine(false);
    $bplot = new BarPlot($yvalues);
    $graph->Add($bplot);
    $bplot->SetColor("darkgreen");
    $bplot->SetFillColor("darkgreen");
    $graph->title->Set($title);
    if (strlen($xtitle) > 0) {
        $graph->xaxis->SetTitle($xtitle, 'middle');
        $graph->xaxis->title->Align('center');
    }
    return $graph;
}

function plotBars($title, $labels, $values) {
	$graph = new Graph(40 + 9 * count($values), 400, 'auto');
	$graph->SetScale("textlin");
	$theme_class=new UniversalTheme;
	$graph->SetTheme($theme_class);
	$graph->title->Set($title);
	$graph->SetMargin(80,40,50,40);
	$graph->SetBox(false);
	$graph->ygrid->SetFill(false);
	$graph->xaxis->SetTickLabels($labels);
	$graph->yaxis->HideLine(false);
	$bplot = new BarPlot($values);
	$bplot->SetWidth(0.8);
	$graph->Add($bplot);
	$graph->SetTickDensity(TICKD_SPARSE);
	$graph->xaxis->HideTicks(false,false);
	$graph->xaxis->SetLabelAngle(90);
    $graph->xaxis->SetFont(FF_ARIAL,FS_NORMAL,7); 
	$bplot->SetColor("white");
	$bplot->SetFillColor("darkgreen");
	return $graph;
}

function plot_spikes($title, $labels, $yvalues, $errors) {
	$graph = new Graph(500,300,'auto');
	$graph->SetScale("textlog");
	$theme_class=new UniversalTheme;
	$graph->SetTheme($theme_class);
	$graph->SetBox(false);
	$graph->ygrid->SetFill(false);
	$graph->yaxis->HideLine(false);
	$bplot = new BarPlot($yvalues);
    $bplot->SetYBase(0.001);
	$errpairs = array();
	foreach ($yvalues as $i => $value) {
		$errpairs[] = $value;
		$errpairs[] = $value + $errors[$i];
	}
	$eplot = new ErrorPlot($errpairs);
	$eplot->SetColor("black");
	$eplot->SetWeight(4);
	$bplot->SetAbsWidth(20);
	$eplot->UseTextScale();
	$eplot->AdjustX(11);
	$graph->xaxis->SetTickLabels($labels);
	$graph->Add($bplot);
	$graph->Add($eplot);
	$bplot->SetColor("white");
	$bplot->SetFillColor("blue");
	$graph->title->Set($title);
	$graph->xaxis->HideTicks(true, true);
    $graph->yaxis->scale->SetAutoMin(0.001);
	$graph->yaxis->HideTicks(false,false);
	return $graph;
}

function plot_reads($title, $labels, $values) {
	$graph = new Graph(500,100 + 25 * count($values),'auto');
	$graph->SetScale("textlin");
	$theme_class=new UniversalTheme;
	$graph->SetTheme($theme_class);
	$graph->Set90AndMargin(200,40,50,40);
	$graph->img->SetAngle(90);
	$graph->SetBox(false);
	$graph->ygrid->SetFill(false);
	$graph->xaxis->SetTickLabels($labels);
	$graph->yaxis->HideLine(false);
	$b1plot = new BarPlot($values);
	$graph->Add($b1plot);
	$graph->SetTickDensity(TICKD_SPARSE);
	$b1plot->SetColor("white");
        $b1plot->value->SetColor('black');
        $b1plot->value->Show();
	$b1plot->SetFillColor(array("darkgreen", "darkgreen", "darkgreen", "darkgreen", "red", "red"));
	$graph->title->Set($title);
	return $graph;
}

function plot_senseantisense($title, $labels, $ysense, $yanti) {
        $width = 500;
        if (count($labels) > 6) $width += (count($labels) - 6) * 18;
	$graph = new Graph($width, 500, 'auto');
	$graph->SetScale("textlin");
	$theme_class=new UniversalTheme;
	$graph->SetTheme($theme_class);
	$graph->SetBox(false);
        $lm = 100;
        if (count($labels) > 15) $lm = 30;
	$graph->SetMargin($lm,20,50,50);
	$graph->ygrid->SetFill(false);
	$graph->xaxis->SetLabelMargin(5);
	$graph->xaxis->SetTickLabels($labels);
	$graph->yaxis->HideLine(false);
	$b1plot = new BarPlot($ysense);
	$b2plot = new BarPlot($yanti);
	$gbplot = new GroupBarPlot(array($b1plot,$b2plot));
	$graph->Add($gbplot);
	$b1plot->SetColor("white");
	$b1plot->SetFillColor("darkgreen");
	$b1plot->SetLegend('Sense hits');

	$b2plot->SetColor("white");
	$b2plot->SetFillColor("red");
	$b2plot->SetLegend('Antisense hits');
	$graph->title->Set($title);
	return $graph;
}

function addTable($title, $values){
    $rownames = "ABCDEFGHIJKLMNOPQRSTUVW";
    $numeric = is_numeric($values[0]) || ($values[0] == "(0)");
	if ($numeric == true) {
        $minval = log(min($values) + 0.01);
		$colspan = log(max($values) + 0.01) - $minval;
		$colfactor = 220.0 / $colspan;
	}
	$ncols = count($values) / 8;
        $width = 58 * $ncols + 100;  
	echo "<table style=\"margin-left:40px;margin-bottom:20px;width:" . $width 
             . "px;table-layout:fixed;font-size:12px;\">\n";
               // style=\"border:1px solid black; border-collapse:collapse;\">\n";
        echo "<tr><th></th>";
	for ($col = 1; $col <= $ncols; $col++) {
	    echo '<th>' . sprintf("%02d", $col) . "</th>";
	}
	echo "</tr>\n";
    for ($row = 0; $row < 8; $row++) {
        echo "<tr><th>" . $rownames[$row] . "</th>";
        for ($col = 0; $col < $ncols; $col++) {
			$val = "-";
		    $idx = $col * 8 + $row;
			if ($idx < count($values))
                $val = $values[$idx];
			$bkgcolor = "#808080";
			if ($numeric == true) {
			    $ci = $colfactor *(log($val + 0.01) - $minval);
			    $bkgcolor = sprintf("#%02x%02x20", 220 - $ci, $ci);
			}
			echo "<td style=\"background-color:" . $bkgcolor . ";\">" . $val . "</td>"; 
		}
		echo "</tr>\n";
	}
	echo "</table><br />\n";	
}

?>

