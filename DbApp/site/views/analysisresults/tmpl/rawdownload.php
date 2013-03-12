<?php
defined('_JEXEC') or die('Restricted access');


  echo "<h1>Analysis Result Download for raw counts and normalized data</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

$filePath = "";
$tmpPath  = "/srv/www/htdocs/joomla16/tmp/";
$pats = array("RPM.tab", "RPKM.tab", "MolsNormalized.tab", "ReadsPerMillion.tab", "ReadsPerKBasesPerMillion.tab");
$analysisid = JRequest::getVar("analysisid", "");
$rpmcsv = $expcsv = "";
foreach ($this->items as $result) {
  if ($result->id == $analysisid) {
    $filePath = $result->resultspath;
    $dirs = explode("/", $filePath);
    $sampleId = $dirs[3];
    foreach ($pats as $pat) {
      $rpmName = $sampleId . "_" . $pat;
      $rpmPath = $filePath . "/" . $rpmName;
      if (file_exists($rpmPath)) {
        $rpmcsv = $tmpPath . $rpmName;
        break;
      }
    }
    $expName = $sampleId . "_expression.tab";
    $expPath = $filePath . "/" . $expName;
    if (file_exists($expPath)) {
      $expcsv = $tmpPath . $expName;
    }
  }
}

if ($rpmcsv == "" && $expcsv == "") {
  echo "<p>Can neither locate raw expression nor normalized data. Is the sample analyzed?</p>";
} else {
  if ($rpmcsv != "") {
    if (copy($rpmPath, $rpmcsv)) {
      echo "<br /><br />Right-click this link to save normalized data to your computer: <a href=http://192.168.1.12/joomla16/tmp/$rpmName >$rpmName</a>\n";
    } else {
      echo "<p>Copy of $rpmPath failed - ask the system administrator for help.</p>";
    }
  }
  if ($expcsv != "") {
    if (copy($expPath , $expcsv)) {
      echo "<br /><br />Right-click this link to save raw counts to your computer: <a href=http://192.168.1.12/joomla16/tmp/$expName>$expName</a>\n";
    } else {
      echo "<p>Copy of $expPath failed - ask the system administrator for help.</p>";
    }
  }
}
?>

