<?php
$xmlfile=$argv[1]; //"L152_summary.xml";
$projdir = dirname($xmlfile);
$htmlimgdir = $projdir . "/summaryimg";
mkdir($htmlimgdir);
$outfile = $projdir . "/summary.html";
fclose(STDOUT);
fclose(STDERR);
$STDOUT = fopen($outfile, 'wb');
$STDERR = fopen('php_errors.txt', 'ab');

$imgNo = 1;

require_once ('plotfn.php');

function parseCurves($graphdata) {
    $title = $graphdata->getElementsByTagname("title")->item(0)->nodeValue;
	$title = str_replace("#br#", "\n", $title);
    $xtitle = "";
	$xt = $graphdata->getElementsByTagname("xtitle");
	if ($xt != null && $xt->length > 0)
	    $xtitle = $xt->item(0)->nodeValue;
	$xtitle = str_replace("#br#", "\n", $xtitle);
	$curves = array();
    foreach ($graphdata->getElementsByTagname("curve") AS $curve) {
	    $yaxis = "left";
		if ($curve->hasAttribute("yaxis"))
		    $yaxis = $curve->getAttribute("yaxis");
	    $color = $curve->getAttribute("color");
	    $legend = $curve->getAttribute("legend");
		$xvalues = array();
		$yvalues = array();
		$errors = array();
		foreach ($curve->getElementsByTagname("point") as $point) {
			$xvalues[] = str_replace("#br#", "\n", $point->getAttributeNode("x")->value);
			$yvalues[] = $point->getAttributeNode("y")->value;
                        if ($point->hasAttribute("error"))
			    $errors[] = $point->getAttributeNode("error")->value;
	    }
        $curve = array("legend" => $legend, "color" => $color, "yaxis" => $yaxis,
		               "xvalues" => $xvalues, "yvalues" => $yvalues, "errors" => $errors);
	    $curves[] = $curve;
    }
	return array("title" => $title, "curves" => $curves, "xtitle" => $xtitle);
}

function parseSingleCurve($graphdata) {
    $title = $graphdata->getElementsByTagname("title")->item(0)->nodeValue;
	$title = str_replace("#br#", "\n", $title);
    $xtitle = "";
	$xt = $graphdata->getElementsByTagname("xtitle");
	if ($xt != null && $xt->length > 0)
	    $xtitle = $xt->item(0)->nodeValue;
	$xtitle = str_replace("#br#", "\n", $xtitle);
    $xvalues = array();
    $yvalues = array();
    $y2values = array();
    $errors = array();
    foreach ($graphdata->getElementsByTagname("point") as $point) {
        $xvalues[] = str_replace("#br#", "\n", $point->getAttributeNode("x")->value);
        $yvalues[] = $point->getAttributeNode("y")->value;
        if ($point->hasAttribute("y2"))
            $y2values[] = $point->getAttributeNode("y2")->value;
        if ($point->hasAttribute("error"))
            $errors[] = $point->getAttributeNode("error")->value;
   }
   return array("title" => $title, "xvalues" => $xvalues, "yvalues" => $yvalues,
                "errors" => $errors, "y2values" => $y2values, "xtitle" => $xtitle);
}

function parsePlate($platedata) {
    $title = "Sample distribution of " . $platedata->getAttributeNode("section")->value;
	$values = array();
    foreach ($platedata->getElementsByTagname("d") as $d)
	    $values[] = $d->textContent;
	return array("title" => $title, "values" => $values);	
}

function addGraph($graph) {
    global $imgNo, $htmlimgdir, $projdir;
    $imgNo += 1;
    $file = "summaryimg/graph" . $imgNo . ".png";
    $path = $projdir . "/" . $file;
	$graph->title->SetFont(FF_ARIAL, FS_BOLD, 10); 
	$graph->Stroke($path);
    echo "<img src=\"$file\" alt=\"graph\" /><br />\n"; 
}

$xmlDoc = new DOMDocument();
$xmlDoc->load($xmlfile);
$allxml = $xmlDoc->documentElement;
$project = $allxml->getAttributeNode("project")->value;
echo "<html>";
echo "<head>\n<title>" . $project . "</title>\n";
echo "<style type=\"text/css\">";
echo "table, th, td { border: 1px solid black; border-collapse:collapse;\n";
//echo "                margin-left: auto; margin-right: auto;";
echo "}\n";
echo "td { text-align: right; }\n";
echo "</style>\n";
echo "</head>\n";
echo "<body>\n";
echo "<p><b>Summary of results in $project.</b></p>\n";
foreach ($allxml->childNodes AS $graphdata) {
    switch ($graphdata->nodeName) {
       case "reads":
         $d = parseSingleCurve($graphdata);
         $graph = plot_reads($d["title"], $d["xvalues"], $d["yvalues"]);
         addGraph($graph);
         break;
       case "features":
         $d = parseSingleCurve($graphdata);
         $graph = plot_reads($d["title"], $d["xvalues"], $d["yvalues"]);
         addGraph($graph);
         break;
      case "spikes":
         $d = parseSingleCurve($graphdata);
         $graph = plot_spikes($d["title"], $d["xvalues"], $d["yvalues"], $d["errors"]);
	     addGraph($graph);
         break;
       case "spikedetection":
         $d = parseCurves($graphdata);
         $graph = plotLines("textlin", "log", $d["title"], $d["curves"], $d["xtitle"]);
         addGraph($graph);
         break;
       case "librarydepth":
         $d = parseCurves($graphdata);
         $graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
         addGraph($graph);
         break;
       case "cvhistogram":
         $d = parseSingleCurve($graphdata);
         $graph = plotHistogram($d["title"], $d["xtitle"], $d["xvalues"], $d["yvalues"]);
         addGraph($graph);
         break;
      case "senseantisense":
         $d = parseSingleCurve($graphdata);
         $graph = plot_senseantisense($d["title"], $d["xvalues"], $d["yvalues"], $d["y2values"]);
		 addGraph($graph);
		 break;
      case "hitprofile":
	    $d = parseCurves($graphdata);
		$graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
		addGraph($graph);
		break;
      case "variationbyreads":
	    $d = parseCurves($graphdata);
		$graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
		addGraph($graph);
		break;
    }
}
$xlabels = null;
foreach ($allxml->getElementsByTagName("barcodestat") as $platedata) {
    if ($platedata->getAttributeNode("section")->value == "wellids") {
	    $d = parsePlate($platedata);
	    $xlabels = $d["values"];
	    break;
    }
}
if ($xlabels != null) {
	foreach ($allxml->getElementsByTagName("barcodestat") as $platedata) {
	    $d = parsePlate($platedata);
	    if (is_numeric($d["values"][0])) {
	  	    $graph = plotBars($d["title"], $xlabels, $d["values"]);
		    addGraph($graph);
			addTable($d["title"], $d["values"]);
	    }
	}
}
echo "</body>\n</html>\n";
fclose(STDOUT);
fclose(STDERR);
?>
