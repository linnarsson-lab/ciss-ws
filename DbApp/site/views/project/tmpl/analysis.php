<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  jimport( 'joomla.filesystem.path' );
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
//  $analysid = JRequest::getVar('analysid') ;
  $analys = $this->theanalysis;
  $document = JFactory::getDocument();
  $document->setTitle(JText::_($analys->plateid . ' result summary'));
  $xmlfile = $analys->resultspath . "/" . $analys->plateid . "_summary.xml";
  if (! file_exists($xmlfile)) {
    echo "<p>The requested analysis file $xmlfile has been removed!";
    JError::raiseError('Error!', 'The requested analysis file ' . $xmlfile . ' has been removed!');
}

$imgNo = 2600;
require_once ('plotfn.php');

function parseCurves($graphdata, $filter=null) {
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
            $xval = str_replace("#br#", "\n", $point->getAttributeNode("x")->value);
            if ($filter != null && !preg_match($filter, $xval))
                continue;
            $xvalues[] = $xval;
            $yvalues[] = $point->getAttributeNode("y")->value;
            $errors[] = ($point->hasAttribute("error"))? $point->getAttributeNode("error")->value : "";
        }
        $curve = array("legend" => $legend, "color" => $color, "yaxis" => $yaxis,
                       "xvalues" => $xvalues, "yvalues" => $yvalues, "errors" => $errors);
        $curves[] = $curve;
    }
    return array("title" => $title, "curves" => $curves, "xtitle" => $xtitle);
}

function parseSingleCurve($graphdata, $filter=null) {
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
        $xval = str_replace("#br#", "\n", $point->getAttributeNode("x")->value);
        if ($filter != null && !preg_match($filter, $xval))
            continue;
        $xvalues[] = $xval;
        $yvalues[] = $point->getAttributeNode("y")->value;
        $y2values[] = ($point->hasAttribute("y2"))? $point->getAttributeNode("y2")->value : "";
        $errors[] = ($point->hasAttribute("error"))? $point->getAttributeNode("error")->value : "";
   }
   return array("title" => $title, "xvalues" => $xvalues, "yvalues" => $yvalues,
                "errors" => $errors, "y2values" => $y2values, "xtitle" => $xtitle);
}

function parsePlate($platedata) {
    $title = "Sample distribution of " . $platedata->getAttributeNode("section")->value;
    $values = array();
    foreach ($platedata->getElementsByTagname("d") as $d) {
        $values[] = $d->textContent;
    }
    return array("title" => $title, "values" => $values);	
}

function addGraph($graph) {
    global $imgNo;
    $imgNo += 1;
    $file = "tmp/graph" . $imgNo . ".png";
    $graph->title->SetFont(FF_ARIAL, FS_BOLD, 10); 
    $graph->Stroke($file);
    echo "<img src=\"$file\" alt=\"graph\" /><br />\n"; 
}

$xmlDoc = new DOMDocument();
$xmlDoc->load($xmlfile);
$allxml = $xmlDoc->documentElement;
$project = $allxml->getAttributeNode("project")->value;
echo "<style type=\"text/css\">";
echo "table, th, td { border: 1px solid black; border-collapse:collapse;font-family:\"Courier New\", Courier, monospace;}\n";
echo "th { text-align: center; }\n";
echo "td { text-align: right; font-weight:bold; padding-right:5px; }\n";
echo "</style>\n";
echo "<p><b>Summary of results in $project.</b></p>\n";
foreach ($allxml->childNodes AS $graphdata) {
    switch ($graphdata->nodeName) {
       case "readfiles":
         echo "<p><b>The following read files were analyzed:</b><br />\n";
         foreach ($graphdata->getElementsByTagname("readfile") as $readfile) {
           $rfpath = $readfile->getAttributeNode("path")->value;
           echo "&nbsp;&nbsp;" . $rfpath . "<br />\n";
         }
         echo "</p>\n";
         break;
       case "reads":
         $d = parseSingleCurve($graphdata);
         $graph = plot_reads($d["title"], $d["xvalues"], $d["yvalues"]);
         addGraph($graph);
         break;
       case "molecules":
       case "hits":
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
         $d = parseSingleCurve($graphdata, "/#[T0-9]/");
         if (count($d["xvalues"]) > 0) {
           $graph = plot_spikes($d["title"], $d["xvalues"], $d["yvalues"], $d["errors"]);
           addGraph($graph);
         }
         break;
       case "spikedetection":
         $d = parseCurves($graphdata, "/#[T0-9]/");
         if (count($d["curves"][0]["xvalues"]) > 1) {
           $graph = plotLines("textlin", "log", $d["title"], $d["curves"], $d["xtitle"]);
           addGraph($graph);
         }
         break;
       case "librarycomplexity":
         echo "<div style=\"width:500px;text-align:center;font-family:arial,sans-serif;\">"
              . "<p style=\"text-align:center;font-weight:bold;font-size:0.8em;\">Estimations of library depth</p>\n";
         $title = $graphdata->getElementsByTagname("title")->item(0)->nodeValue;
         echo "<p style=\"font-style:italic;font-size:0.6em;\">$title</p>\n"
              . "<p style=\"font-size:0.8em;\">";
         foreach ($graphdata->getElementsByTagname("point") as $point) {
           $measure = $point->getAttributeNode("x")->value;
           $value = $point->getAttributeNode("y")->value;
           echo "&nbsp;&nbsp;$measure:&nbsp;$value<br />\n";
         }
         echo "<br /></p></div>\n";
         break;
       case "fracuniqueperlane":
         $d = parseCurves($graphdata);
         if (count($d["curves"][0]["xvalues"]) > 1) {
           $graph = plotLines("textlin", "", $d["title"], $d["curves"], $d["xtitle"]);
           addGraph($graph);
         }
         break;
       case "librarydepth":
         $d = parseCurves($graphdata);
         $graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
         addGraph($graph);
         break;
       case "librarydepthbybc":
         $d = parseCurves($graphdata);
         $graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
         addGraph($graph);
         break;
       case "transcriptdepthbybc":
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
      case "senseantisensebychr":
         $d = parseSingleCurve($graphdata);
         $graph = plot_senseantisense($d["title"], $d["xvalues"], $d["yvalues"], $d["y2values"]);
         addGraph($graph);
         break;
      case "hitprofile":
        $d = parseCurves($graphdata);
        if (count($d["curves"]) > 0) {
          $graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
          addGraph($graph);
        }
        break;
      case "variationbyreads":
        $d = parseCurves($graphdata);
        $graph = plotLines("linlin", "", $d["title"], $d["curves"], $d["xtitle"]);
        addGraph($graph);
        break;
      case "randomtagfrequence":
        $d = parseSingleCurve($graphdata);
        $graph = plotBarsByCategory("textlin", $d["title"], $d["xtitle"], $d["xvalues"], $d["yvalues"]);
        addGraph($graph);
        break;
      case "nuniqueateachrandomtagcoverage":
        $d = parseSingleCurve($graphdata);
        $graph = plotBarsByCategory("textlog", $d["title"], $d["xtitle"], $d["xvalues"], $d["yvalues"]);
        addGraph($graph);
        break;
      case "moleculereadscountshistogram":
        $d = parseSingleCurve($graphdata);
        $graph = plotBarsByCategory("textlog", $d["title"], $d["xtitle"], $d["xvalues"], $d["yvalues"]);
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
        if (is_numeric($d["values"][0]) || preg_match('/\([0-9]+\)/', $d["values"][0]) == 1) {
            $numvals = array();
            foreach ($d["values"] as $val) {
              $numvals[] = (preg_match('/\([0-9]+\)/', $val) == 1)? "0" : $val;
            }
            $graph = plotBars($d["title"], $xlabels, $numvals);
            addGraph($graph);
        }
        addTable($d["title"], $d["values"]);
    }
}
?>

