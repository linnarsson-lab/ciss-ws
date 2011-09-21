<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  echo "<h1>Illumina runs</h1>";
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;

  $newlink = "<a href=index.php?option=com_dbapp&view=illuminarun&layout=edit&controller=illuminarun&searchid=0&Itemid="
             . $itemid . " >&nbsp;Add&nbsp;new&nbsp;Illumina&nbsp;run&nbsp;</a>";
  $sorturlhead = "<a href=index.php?option=com_dbapp&view=illuminaruns&layout=default&Itemid="
                 . $itemid . "&sortKey=";
  $runidsorter = ($sortKey == "runid")? "RunId" : ($sorturlhead . "runid>RunId</a>");
  $runnosorter = ($sortKey == "runno")? "RunNo" : ($sorturlhead . "runno>RunNo</a>");
  $datesorter = ($sortKey == "date")? "Newest first" : ($sorturlhead . "date>Newest first</a>");

  echo "<div class='illuminarun'><fieldset>
         <legend><nobr> $newlink </nobr><br /><br />
                 <nobr> Sort: $runidsorter $runnosorter $datesorter </nobr>
         </legend>
         <table>
           <tr><th colspan='2'></th>
            <th>RunId&nbsp;</th>
            <th>Title&nbsp;<br />" . JHTML::tooltip('Your free designation of the run') . "</th>
            <th><nobr>Run date&nbsp;</nobr></th>
            <th>Status&nbsp;<br />" . JHTML::tooltip('n/a=No data exists, copying=making read files, copied=ready for analysis, copyfail=error during read file making') . "</th>
            <th>RunNo&nbsp;<br />" . JHTML::tooltip('Run number created by Illumina machine') . "</th>
            <th>Run doc&nbsp;</th>
            <th>Samples&nbsp;</th>    
           </tr>"; 

  function datesort($a, $b) { if ($a->rundate == $b->rundate) { return 0; }
                                return ($a->rundate > $b->rundate) ? -1 : 1; };
  function runidsort($a, $b) { if ($a->illuminarunid == $b->illuminarunid) { return 0; }
                                return ($a->illuminarunid > $b->illuminarunid) ? -1 : 1; };
  function runnosort($a, $b) { if ($a->runno == $b->runno) { return 0; }
                                return ($a->runno > $b->runno) ? -1 : 1; };
  if ($sortKey == "date") {
    usort($this->illuminaruns, "datesort");
  } else  if ($sortKey == "runid") {
    usort($this->illuminaruns, "runidsort");
  } else  if ($sortKey == "runno") {
    usort($this->illuminaruns, "runnosort");
  }

  foreach ($this->illuminaruns as $run) {
    echo "<tr>";
    $runlink = "<a href=index.php?option=com_dbapp&view=illuminarun&layout=illuminarun&controller=illuminarun&searchid=" 
               . $run->id . "&Itemid=" . $itemid . ">view</a>&nbsp;";
    $editlink = "<a href=index.php?option=com_dbapp&view=illuminarun&layout=edit&controller=illuminarun&searchid=" 
           . $run->id . "&Itemid=" . $itemid . ">edit</a>&nbsp;";
    echo "<td>&nbsp;" . $runlink . "</td><td>" . $editlink . "</td>";
    echo "<td><nobr>&nbsp;" . $run->illuminarunid . "</nobr></td>";
    echo "<td><nobr>&nbsp;" . $run->title . "</nobr></td>";
    echo "<td><nobr>&nbsp;" . $run->rundate . "</nobr></td>";
    echo "<td><nobr>&nbsp;" . $run->status . "</nobr></td>";
    echo "<td><nobr>&nbsp;" . $run->runno . "</nobr></td>";
    $RUNDOC = "";
    if ($run->rundocument != "") {
      $RUNDOC = "<nobr>&nbsp;<a href='../../../../../../uploads/" . $run->rundocument . "' target='_blank' >Yes</a></nobr>&nbsp;";
    }
    echo "<td>&nbsp;" . $RUNDOC . "</td>";

    $plateids = explode(',', $run->plateids);
    $platedbids = explode(',', $run->platedbids);
    $vplatedbids = array();
    foreach ($platedbids as $bid)
        if ($bid != "1") $vplatedbids[] = $bid;
    $platelinks = array();
    for ($i = 0; $i < sizeof($plateids); $i++) {
      $platelinks[] = " <a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
                      . $vplatedbids[$i] . "&Itemid=" . $itemid . ">" . $plateids[$i] . "</a>";
    }
    $platelinks = implode(',', $platelinks);
    echo "<td>$platelinks</td>";
    //echo "<td>&nbsp;" . $run->plateids . "</td>";
    //echo "<td><nobr>&nbsp;" . $run->user . "&nbsp; " ;
    //echo $run->time . "</nobr></td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />";

?>

