<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<h1>Analysis results</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

  $sorturlhead = "<a href=index.php?option=com_dbapp&view=analysisresults&layout=default&Itemid="
                 . $itemid . "&sortKey=";
  $newestsorter = ($sortKey == "newest")? "Newest first" : ($sorturlhead . "newest>Newest first</a>");
  $sampleidsorter = ($sortKey == "sampleid")? "SampleId" : ($sorturlhead . "sampleid>SampleId</a>");

  echo "<div class='analysis'><fieldset>
         <legend><nobr>Requested and processed analysis</nobr><br /<br />
                <nobr> Sort: $newestsorter $sampleidsorter </nobr>
         </legend>
         <table>
         <tr>
          <th>&nbsp;</th>
          <th>SampleId&nbsp;</th>
          <th>Status&nbsp;</th>
          <th>Path<br />" . JHTML::tooltip('Not displayed until results are ready') . "&nbsp;</th>
          <th>Lanes<br />" . JHTML::tooltip('Total no. of lanes included in analysis') . "&nbsp;</th>
          <th>Extr<br />" . JHTML::tooltip('Version of read filter and barcoded extraction software') . "&nbsp;</th>
          <th>Annot<br />" . JHTML::tooltip('Version of feature annotation software') . "&nbsp;</th>
          <th>Genome<br />" . JHTML::tooltip('Not displayed until results are ready') . "&nbsp;</th>
          <th>DBVer<br />" . JHTML::tooltip('Source and creation date of genome and annotation database. Note that source may have changed if the requested build was not available at processing time.') . "&nbsp;</th>
          <th>Type<br />" . JHTML::tooltip('all=known transcript variants analyzed separately, single=one value for each locus') . "&nbsp;</th>
          <th>Com-<br />ment</th>
         </tr>";

    function newestsort($a, $b) { if ($a->id == $b->id) { return 0; }
                                  return ($a->id > $b->id) ? -1 : 1; };
    function sampleidsort($a, $b) { if ($a->project == $b->project) { return 0; }
                                  return ($a->project > $b->project) ? -1 : 1; };
  if ($sortKey == "newest") {
    usort($this->items, "newestsort");
  } else if ($sortKey == "sampleid") {
    usort($this->items,"sampleidsort");
  }

  foreach ($this->items as $result) {
    $rpath = $result->resultspath;
    if (!file_exists($rpath) && $result->status == "ready") {
      $rpath = "<i> $rpath - missing! </i>"; 
    }
    $viewlink = "";
    if (file_exists($rpath) && $result->status == "ready") {
      $viewlink = "&nbsp;<a href=index.php?option=com_dbapp&view=project&controller=project&layout=analysis&searchid="
                  . $result->id . "&Itemid=" . $itemid . ">view</a>";
    }
    $projectlink = "&nbsp;<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
           . $result->projectid . "&Itemid=" . $itemid . ">" . $result->project . "</a>";
    echo "\n    <tr>";
    echo "<td>" . $viewlink . "&nbsp;</td>";
    echo "<td>" . $projectlink . "&nbsp;</td>"; 
    echo "<td>" . $result->status . "</td>";
    echo "<td>" . $rpath . "&nbsp;</td>";
    echo "<td>" . $result->lanecount . "&nbsp;</td>";
    echo "<td>" . $result->extraction_version . "&nbsp;</td>";
    echo "<td>" . $result->annotation_version . "&nbsp;</td>";
    echo "<td>" . $result->genome . "&nbsp;</td>";
    echo "<td>" . $result->transcript_db_version . "&nbsp;</td>";
    echo "<td>" . $result->transcript_variant . "&nbsp;</td>";
    echo "<td>" . ( (strlen($result->comment) > 4)? JHTML::tooltip($result->comment) :  $result->comment ) . "</td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />&nbsp;<br />";

?>

