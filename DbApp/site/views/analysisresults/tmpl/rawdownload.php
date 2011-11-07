<?php
defined('_JEXEC') or die('Restricted access');


  echo "<h1>Analysis Result Download for RPM and raw</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

$filePath = "";
$tmpPath  = "/srv/www/htdocs/joomla16/tmp/";
if (1) {
  $analysisid = JRequest::getVar("analysisid", "");
  foreach ($this->items as $result) {
    if ($result->id == $analysisid) {
      $filePath = $result->resultspath;
      $dirs = explode("/", $filePath);
//      $fileName = $filePath . "/" . $dirs[3] . "_RPM.tab";
      $rpmName = $filePath . "/" . $dirs[3] . "_RPM.tab";
      $rpmcsv = $tmpPath . $dirs[3] . "_RPM.tab";
      $expName = $filePath . "/" . $dirs[3] . "_expression.tab";
      $expcsv = $tmpPath . $dirs[3] . "_expression.tab";
    }
  }

  if ((copy($rpmName, $rpmcsv)) && (copy($expName , $expcsv))) {
    echo "<br /><br />To download right-click this link and save the file to your computer <a href=http://192.168.1.12/joomla16/tmp/" . $dirs[3] . "_RPM.tab >" . $dirs[3] . "_RPM.tab</a>";
    echo "<br /><br />To download right-click this link and save the file to your computer <a href=http://192.168.1.12/joomla16/tmp/" . $dirs[3] . "_expression.tab >" . $dirs[3] . "_expression.tab</a>";
  } else {
    echo "<p>Copy failed are the results analyzed? Ask the system administrator.</p>";
  }
}

?>

