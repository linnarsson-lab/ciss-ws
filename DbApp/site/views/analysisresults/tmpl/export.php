<?php
defined('_JEXEC') or die('Restricted access');
require_once ('strt2Qsingle.php');
// createqlucore.php
?>


<?php
  echo "<h1>STRT data export/download page</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
//  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

  echo "<div class='analysis'><fieldset>
         <form  name=input  action='index.php?option=com_dbapp&view=analysisresults&layout=joindata'  method=post   ><table>";
  echo "<p>To download <b>Standard results files [.tab]</b> just click the link in the analysis column.</p>";
  echo "<p>To download data for <b>Qlucore [.gedata]</b> tick the boxes from relevant runs and click a submit button. You can only combine data from one genome type (identified by sub-tables), variables refer to gene count.</p>";
  echo "<p>N.B.: If you want to combine data, but they are within groups with different number of variables, it means they have been run against different database versions. Check up under the samples and rerun analysis of the older one.</p>";
  $outstrings  = array();
  $count = 0;
  foreach ($this->items as $result) {
    if ($result->status == "cancelled")
        continue;
    $filePath = $result->resultspath;
    $dirs = explode("/", $filePath);
    $gnms = explode("_", $dirs[count($dirs) - 1]);
    $sampleId = $dirs[count($dirs) - 2];
    $qlucoreFile = $sampleId . "_RPM.gedata";
    if (!file_exists($filePath . "/" . $qlucoreFile))
      $qlucoreFile = $sampleId . "_RPKM.gedata";
    if (file_exists($filePath . "/" . $qlucoreFile)) {
      $count++;
      $fh = fopen($filePath . "/" . $qlucoreFile, 'r');
      $line = stream_get_line($fh, 100, "\n\n");
      $line = stream_get_line($fh, 100, "\n\n");
      preg_match_all('(\d+)', $line, $out);
      if (count($gnms) >= 5) {
        $gnm = $gnms[count($gnms) - 4];
      } else {
        $gnm = (preg_match('/^v[0-9].*/', $gnms[1]) == 0 || $gnms[1] != 'TruSeq')? $gnms[1] : $gnms[2];
      }
      $outstring = "<tr><td colspan=7 bgcolor=yellow>Genome <b>" . $gnm . "</b> Variables <b>" . $out[0][2] . "</b></td></tr>_CUT_";
      $outstring .= "<tr><th>" . $qlucoreFile . "</th>";
      $outstring .= "<td align=center><input type=checkbox name=file" . $count . " value='" . $filePath . "/" . $qlucoreFile . "' /></td>";
      $outstring .= "<td><a href=index.php?option=com_dbapp&view=analysisresults&layout=rawdownload&analysisid=" . $result->id . "  \"target=_blank\" >" . $dirs[4] . "</a></td>";
      $outstring .= "<td align=center>" . $out[0][0] . "</td>";
      $outstring .= "<td align=center>" . $out[0][1] . "</td>";
      $outstring .= "<td align=center>" . $out[0][2] . "</td>";
      $outstring .= "<td align=center>" . $out[0][3] . "</td></tr>";
      array_push($outstrings, $outstring);
    }
  }
  sort($outstrings);
  $outrow = "";
  foreach ($outstrings as $string) {
    $parts = explode("_CUT_", $string);
    if ($outrow != $parts[0]) {
      $outrow = $parts[0];
      echo "<tr><td colspan=7 align=right ><input type=submit value=Submit /></td>" . $outrow;
      echo "<tr><th>Run</th><th>Export</th><th>Analysis</th><th>Samples</th><th>Attributes</th><th>Variables</th><th>Annotations</th></tr>";
      echo $parts[1];
    } else {
      echo $parts[1];
    }
  }
  echo "</table></form>";
// . count ($outstrings);

?>
