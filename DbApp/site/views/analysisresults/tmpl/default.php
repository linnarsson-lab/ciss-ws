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
         <legend><nobr>Requested and processed analyses</nobr><br /<br />
                <nobr> Sort: $newestsorter $sampleidsorter </nobr>
         </legend>
         <table>
         <tr>
          <th>&nbsp;</th>
          <th>Sample</th>
          <th>Status<br />" . JHTML::tooltip('If "ready" - link to export in Qlucore format [.gedata]') . "</th>
          <th>Path<br />" . JHTML::tooltip('Not displayed until results are ready') . "</th>
          <th>L<br />" . JHTML::tooltip('Total no. of lanes included in analysis') . "</th>
          <th>Extr<br />" . JHTML::tooltip('Version of read filter and barcoded extraction software') . "</th>
          <th>Ann<br />" . JHTML::tooltip('Version of feature annotation software') . "</th>
          <th>RPKM</th>
          <th>Gnm<br /> " . JHTML::tooltip('Not displayed until results are ready') . "</th>
          <th>DBVer<br />" . JHTML::tooltip('Source and creation date of genome and annotation database. Note that source may have changed if the requested build was not available at processing time.') . "</th>
          <th>Type<br />" . JHTML::tooltip('all=known transcript variants analyzed separately, single=one value for each locus') . "</th>
          <th>Cmnt</th>
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
    if ($result->status == "cancelled")
        continue;
    $rpath = $result->resultspath;
    $cpath = $result->resultspath;
    $qlink = "<td>" . $result->status . "&nbsp;</td>";
    $viewlink = "";
    if ($result->status == "ready") {
      if (!file_exists($rpath)) {
        $rpath = "<i>" . pathinfo($rpath, PATHINFO_BASENAME) . " - missing! </i>"; 
      } else {
        $rpath = pathinfo($rpath, PATHINFO_BASENAME);
        $viewlink = "&nbsp;<a href=index.php?option=com_dbapp&view=project&controller=project&layout=analysis&searchid="
                    . $result->id . "&Itemid=" . $itemid . ">view</a>";
        $qlink = "<td><a href=index.php?option=com_dbapp&view=analysisresults&layout=download&analysisid=" . $result->id . "  \"target=_blank\" >" . $result->status . "</a></td>";
      }
    }
    if (strlen($result->project) < 8) {
      $projectlink = "&nbsp;<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
           . $result->projectid . "&Itemid=" . $itemid
           . "\" title=\"$result->projecttitle / $result->tissue / $result->sampletype\">" . $result->project . "</a>";
    } else {
      $projectlink = "&nbsp;<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
           . $result->projectid . "&Itemid=" . $itemid
           . "\" title=\"$result->projecttitle / $result->tissue / $result->sampletype\">" . substr($result->project, 0, 5) . "...</a>";
    }
    echo "\n    <tr>";
    echo "<td>" . $viewlink . "&nbsp;</td>";
    echo "<td>" . $projectlink . "&nbsp;</td>"; 
    echo $qlink;
    if (strlen($cpath) > 0)
      echo "<td><nobr>" . JHTML::tooltip($cpath) . " &nbsp; " . $rpath . "</nobr></td>";
    else
      echo "<td><nobr>&nbsp;&nbsp;&nbsp;" . $rpath . "</nobr></td>";
    echo "<td>" . $result->lanecount . "&nbsp;</td>";
    echo "<td>" . $result->extraction_version . "&nbsp;</td>";
    echo "<td>" . $result->annotation_version . "&nbsp;</td>";
    echo "<td>" . (($result->rpkm == "1")? "Yes" : "---") . "&nbsp;</td>";
    echo "<td>" . $result->genome . "&nbsp;</td>";
    echo "<td>" . $result->transcript_db_version . "&nbsp;</td>";
    echo "<td>" . $result->transcript_variant . "&nbsp;</td>";
    echo "<td>" . ( (strlen($result->comment) > 4)? JHTML::tooltip($result->comment) :  $result->comment ) . "</td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />&nbsp;<br />";

?>

