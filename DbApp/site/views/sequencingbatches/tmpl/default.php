<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $signedStatus = JRequest::getVar('signedStatus', "");
  $invoiceStatus = JRequest::getVar('invoiceStatus', "");
  $assignedStatus = JRequest::getVar('assignedStatus', "");
  $clientId = JRequest::getVar('clientId', "");
  $sortKey = JRequest::getVar('sortKey', "");

  $newlink = "&nbsp;<a href=index.php?option=com_dbapp&view=sequencingbatch&layout=edit&controller=sequencingbatch&searchid=0&Itemid=" . $itemid . " >Add&nbsp;new&nbsp;batch</a>&nbsp;&nbsp;";
  $otherinvoicestatus = ($invoiceStatus == "not invoiced")? "invoiced" : "not invoiced";
  $changeinvoicelink =
         "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&Itemid=" . $itemid
         . "&assignedStatus=" . urlencode($assignedStatus) . "&invoiceStatus=" . urlencode($otherinvoicestatus)
         . "&signedStatus=" . urlencode($signedStatus) . "&clientId=" . urlencode($clientId) . "&sortKey="
         . urlencode($sortKey) . ">Show " . $otherinvoicestatus . "</a>&nbsp;";

  $othersignedstatus = ($signedStatus == "unsigned")? "signed" : "unsigned";
  $changesignedlink =
         "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&Itemid=" . $itemid
         . "&assignedStatus=" . urlencode($assignedStatus) . "&invoiceStatus=" . urlencode($invoiceStatus)
         . "&signedStatus=" . urlencode($othersignedstatus) . "&clientId=" . urlencode($clientId) . "&sortKey=" 
         . urlencode($sortKey) . ">Show " . $othersignedstatus . "</a>&nbsp;";

  $otherassignedstatus = ($assignedStatus == "incomplete")? "completed" : "incomplete";
  $changeassignedlink =
         "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&Itemid=" . $itemid
         . "&assignedStatus=" . urlencode($otherassignedstatus) . "&invoiceStatus=" . urlencode($invoiceStatus)
         . "&signedStatus=" . urlencode($signedStatus) . "&clientId=" . urlencode($clientId) . "&sortKey="
         . urlencode($sortKey) . ">Show " . $otherassignedstatus . " only</a>&nbsp;";

  $sorturlhead =
         "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&Itemid=" . $itemid
         . "&assignedStatus=" . urlencode($assignedStatus) . "&invoiceStatus=" . urlencode($invoiceStatus)
         . "&signedStatus=" . urlencode($signedStatus) . "&clientId=" . urlencode($clientId) . "&sortKey=";
  $newestsorter = ($sortKey == "newest")? "Newest first" : ($sorturlhead . "newest>Newest first</a>");
  $samplesorter = ($sortKey == "sample")? "SampleId" : ($sorturlhead . "sample>SampleId</a>");
  $cycles1sorter = ($sortKey == "cycles1")? "Cycles" : ($sorturlhead . "cycles1>Cycles</a>");
  $primer1sorter = ($sortKey == "primer1")? "Primer" : ($sorturlhead . "primer1>Primer</a>");
  $cycles2sorter = ($sortKey == "cycles2")? "Indexcycles" : ($sorturlhead . "cycles2>Indexcycles</a>");
  $primer2sorter = ($sortKey == "primer2")? "Indexprimer" : ($sorturlhead . "primer2>Indexprimer</a>");
  $assignedsorter = ($sortKey == "assigned")? "#Non-assigned" : ($sorturlhead . "assigned>#Non-assigned</a>");
  $pisorter = ($sortKey == "pi")? "P.I." : ($sorturlhead . "pi>P.I.</a>");

  $statusText = "";
  $alllink = "";
  if ($invoiceStatus != "" || $signedStatus != "" || $assignedStatus != "") {
    $alllink = "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&Itemid=" . $itemid
               . "&sortKey=" . urlencode($sortKey) . ">Show all</a>&nbsp;";
  } else $statusText = "all ";
  $filterText = ($clientId == "")? "" : ("for P.I. " . $clientId);
  $statusText .= "$signedStatus $invoiceStatus $assignedStatus sequencing batches $filterText";
  $statusText = ucfirst(trim($statusText));

  echo "<h1> $statusText </h1>";
  echo "<div class='sequencingbatch'>
         <fieldset>
          <legend>
                  <nobr>$newlink $changeinvoicelink $changesignedlink $changeassignedlink $alllink</nobr><br /><br />
                  <nobr>Sort: $newestsorter $assignedsorter $samplesorter $primer1sorter $cycles1sorter
                               $primer2sorter $cycles2sorter $pisorter</nobr>
          </legend>
          <table>
            <tr><th colspan='2'></th>
            <th>&nbsp;SampleId</th>
            <th><nobr>Batch (DB&nbsp;id)&nbsp;</nobr></th>
            <th>#Cycles&nbsp;<br />/Primer</th>
            <th>#IdxCycles&nbsp;<br />/Primer</th>
            <th>Plan&nbsp;<br />" . JHTML::tooltip('Total # lanes that are planned') . "</th>
            <th>Asgn&nbsp;<br />" . JHTML::tooltip('Total # lanes that have been assigned to runs') . "</th>
            <th>IlluminaRuns&nbsp;</th>
            <th>P.I.&nbsp;</th>
            <th>Sgn&nbsp;" . JHTML::tooltip('Has the batch been signed by the P.I.?') . "</th>
            <th>Cost&nbsp;<br />" . JHTML::tooltip('Total cost of the batch') . "</th>
            <th>Inv&nbsp;<br />" . JHTML::tooltip('Has the invoice been sent?') . "</th>
            <th>Com-<br />ment</th>\n";

  function newestsort($a, $b) { if ($a->id == $b->id) { return 0; }
                                return ($a->id > $b->id) ? -1 : 1; };
  function samplesort($a, $b) { if ($a->plateid == $b->plateid) { return 0; }
                                return ($a->plateid > $b->plateid) ? -1 : 1; };
  function primer1sort($a, $b) { if ($a->primer == $b->primer) 
                                   { return ($a->plannednumberofcycles > $b->plannednumberofcycles) ? -1 : 1 ; }
                                return ($a->primer > $b->primer) ? -1 : 1; };
  function cycles1sort($a, $b) { if ($a->plannednumberofcycles == $b->plannednumberofcycles)
                                        { return ($a->primer > $b->primer) ? -1 : 1; }
                                return ($a->plannednumberofcycles > $b->plannednumberofcycles) ? -1 : 1; };
  function primer2sort($a, $b) { if ($a->indexprimer == $b->indexprimer)
                                        { return  ($a->plannedindexcycles > $b->plannedindexcycles) ? -1 : 1; }
                                return ($a->indexprimer > $b->indexprimer) ? -1 : 1; };
  function cycles2sort($a, $b) { if ($a->plannedindexcycles == $b->plannedindexcycles)
                                        { return ($a->indexprimer > $b->indexprimer) ? -1 : 1; }
                                return ($a->plannedindexcycles > $b->plannedindexcycles) ? -1 : 1; };
  function pisort($a, $b) { if ($a->principalinvestigator == $b->principalinvestigator) { return 0; }
                                return ($a->principalinvestigator > $b->principalinvestigator) ? -1 : 1; };
  function assignedsort($a, $b) { $an = $a->plannednumberoflanes - $a->assignedlanes;
                                   $bn = $b->plannednumberoflanes - $b->assignedlanes;
                                   if ($an == $bn) { return 0; }
                                   return ($an > $bn) ? -1 : 1; };

  if ($sortKey == "newest") {
    usort($this->sequencingbatches, "newestsort");
  } else if ($sortKey == "sample") {
    usort($this->sequencingbatches, "samplesort");
  } else if ($sortKey == "primer1") {
    usort($this->sequencingbatches, "primer1sort");
  } else if ($sortKey == "cycles1") {
    usort($this->sequencingbatches, "cycles1sort");
  } else if ($sortKey == "primer2") {
    usort($this->sequencingbatches, "primer2sort");
  } else if ($sortKey == "cycles2") {
    usort($this->sequencingbatches, "cycles2sort");
  } else if ($sortKey == "pi") {
    usort($this->sequencingbatches, "pisort");
  } else if ($sortKey == "assigned") {
    usort($this->sequencingbatches, "assignedsort");
  }

  foreach ($this->sequencingbatches as $batch) {
    if ($clientId != "" && $batch->principalinvestigator != $clientId) continue;
    if ($invoiceStatus == "not invoiced" && $batch->invoice == "sent") continue;
    if ($invoiceStatus == "invoiced" && $batch->invoice != "sent") continue;
    if ($signedStatus == "signed" && $batch->signed != "yes") continue;
    if ($signedStatus == "unsigned" && $batch->signed != "no") continue;
    if ($assignedStatus == "completed" && ($batch->plannednumberoflanes - $batch->assignedlanes) > 0) continue;
    if ($assignedStatus == "incomplete" && ($batch->plannednumberoflanes - $batch->assignedlanes) <= 0) continue;

    echo "<tr>";
    $batchviewlink = "&nbsp;  <a href=index.php?option=com_dbapp&view=sequencingbatch&layout=sequencingbatch&controller=sequencingbatch&searchid=" . $batch->id . "&Itemid=" . $itemid . ">view</a>&nbsp;";
    $batcheditlink = "&nbsp;  <a href=index.php?option=com_dbapp&view=sequencingbatch&layout=edit&controller=sequencingbatch&searchid=" . $batch->id . "&Itemid=" . $itemid . ">edit</a>&nbsp;";
//    echo "<td>" . $batch->title . "</td>";
    echo "<td>$batchviewlink</td><td>$batcheditlink</td>";
    $plateviewlink = "&nbsp;  <a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" . $batch->platedbid . "&Itemid=" . $itemid . ">" . $batch->plateid . "</a>&nbsp;";
    echo "<td>$plateviewlink</td>";
    //echo "<td>" . $batch->plateid . "</td>";
    echo "<td>&nbsp;" . $batch->batchno . " (" . $batch->id . ")</td>";
    if ($batch->primer == "") $seqprimer = "?";
    else {
      $seqprimer = explode(' ', $batch->primer);
      $seqprimer = $seqprimer[0];
    }
    $cycles = ($batch->plannednumberofcycles > 0)? $batch->plannednumberofcycles : "?";
    echo "<td>&nbsp;" . $cycles . " / " . $seqprimer . "</td>";
    if ($batch->indexprimer == "") $idxprimer = "?";
    else {
      $idxprimer = explode(' ', $batch->indexprimer);
      $idxprimer = $idxprimer[0];
    }
    $idxcycles = ($batch->plannedindexcycles > 0)? $batch->plannedindexcycles : "?";
    echo "<td>&nbsp;" . $idxcycles . " / " . $idxprimer . "</td>";
    echo "<td>&nbsp;" . $batch->plannednumberoflanes . "</td>";
    echo "<td>&nbsp;" . $batch->assignedlanes . "</td>";
    $runids = explode(',', $batch->illids);
    $rundbids = explode(',', $batch->illdbids);
    $runlinks = array();
    for ($i = 0; $i < sizeof($runids); $i++) {
      $runlinks[] = " <a href=index.php?option=com_dbapp&view=illuminarun&layout=illuminarun&controller=illuminarun&searchid=" . $rundbids[$i] . "&Itemid=" . $itemid . ">" . $runids[$i] . "</a>";
    }
    $runlinks = implode(',', $runlinks);
    echo "<td>&nbsp;$runlinks</td>";
    //echo "<td>" . $batch->illids . "</td>";
    if ($batch->principalinvestigator == "") $pi = "?";
    else {
      $pi = implode(array_map(create_function('$a', 'return $a[0];'), explode(' ', $batch->principalinvestigator))); 
      $pi = "<a href=index.php?option=com_dbapp&view=client&layout=client&controller=client&searchid="
           . $batch->clientid . "&Itemid=" . $itemid . ">" . $pi . "</a>";
    }
    echo "<td>&nbsp;$pi</td>";
    $signed = "?";
    if ($batch->signed == "yes") $signed = "Y";
    else if ($batch->signed == "no") $signed = "n";
    echo "<td>&nbsp;" . $signed . "</td>";
    echo "<td>&nbsp;" . $batch->cost . "</td>";
    $invoice = "?";
    if ($batch->invoice == "sent") $invoice = "Y";
    else if ($batch->invoice == "not sent") $invoice = "n";
    echo "<td>&nbsp;" . $invoice . "</td>";
    $comment = $batch->comment;
    if (strlen($comment) > 0)
      echo "<td>&nbsp;" . JHTML::tooltip($comment) . "</td></tr>";
    else
      echo "<td></td></tr>";
//    echo "<td><nobr> ";# . $batch->user . " &nbsp; " ;
//    echo $batch->time . " </nobr></td>";
    echo "</tr>\n";
  }
  echo "</table></fieldset></div><br />";

?>

