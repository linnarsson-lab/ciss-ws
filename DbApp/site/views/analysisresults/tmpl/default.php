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
          <th><nobr>Status" . JHTML::tooltip('If "ready" - link to export in Qlucore format [.gedata]') . "</nobr></th>
          <th><nobr>Path " . JHTML::tooltip('Not displayed until results are ready') . "</nobr></th>
          <th><nobr>Lanes " . JHTML::tooltip('Total no. of lanes included in analysis') . "</nobr></th>
          <th><nobr>Extr " . JHTML::tooltip('Version of read filter and barcoded extraction software') . "</nobr></th>
          <th><nobr>Annot " . JHTML::tooltip('Version of feature annotation software') . "</nobr></th>
          <th><nobr>Genome " . JHTML::tooltip('Not displayed until results are ready') . "</nobr></th>
          <th><nobr>DBVer " . JHTML::tooltip('Source and creation date of genome and annotation database. Note that source may have changed if the requested build was not available at processing time.') . "</nobr></th>
          <th><nobr>Type " . JHTML::tooltip('all=known transcript variants analyzed separately, single=one value for each locus') . "</nobr></th>
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
    if (!file_exists($rpath) && $result->status == "ready") {
      $rpath = "<i>" . substr($rpath, 19, 26) . " - missing! </i>"; 
      $qlink = "<td>" . $result->status . "</td>";
    } elseif ($result->status != "ready") {
      $qlink = "<td>" . $result->status . "</td>";
    } else {
      $rpath = substr($rpath, 15, 28);
//       $qlink = "<td>" . $result->status . "</td>";
//       add link here for the Qlucore download things
$qlink = "<td><a href=index.php?option=com_dbapp&view=analysisresults&layout=download&analysisid=" . $result->id . "  \"target=_blank\" >" . $result->status . "</a></td>";
    }
    $viewlink = "";
    if (file_exists($rpath) && $result->status == "ready") {
      $viewlink = "&nbsp;<a href=index.php?option=com_dbapp&view=project&controller=project&layout=analysis&searchid="
                  . $result->id . "&Itemid=" . $itemid . ">view</a>";
    }
    if (strlen($result->project) < 7) {
    $projectlink = "&nbsp;<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
           . $result->projectid . "&Itemid=" . $itemid
           . "\" title=\"$result->projecttitle / $result->tissue / $result->sampletype\">" . $result->project . "</a>";
    } else {
    $projectlink = "&nbsp;<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
           . $result->projectid . "&Itemid=" . $itemid
           . "\" title=\"$result->projecttitle / $result->tissue / $result->sampletype\">" . substr($result->project, 1, 5) . "&nbsp&.&nbsp&.</a>";
    }
    echo "\n    <tr>";
    echo "<td>" . $viewlink . "&nbsp;</td>";
    echo "<td>" . $projectlink . "&nbsp;</td>"; 
    echo $qlink;
    echo "<td><nobr>" . JHTML::tooltip($cpath) . " &nbsp; " . $rpath . "</nobr></td>";
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

