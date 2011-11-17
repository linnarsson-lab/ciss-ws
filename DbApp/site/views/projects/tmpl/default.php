<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $clientId = JRequest::getVar('clientId', "");
  $managerId = JRequest::getVar('managerId', "");
  $contactId = JRequest::getVar('contactId', "");
  $cancelled = JRequest::getVar('cancelled', "no");
  $strt = JRequest::getVar('strt', "all");
  $sortKey = JRequest::getVar('sortKey', "");
  $itemid = $menu->id;
  $newlink = "<a href=index.php?option=com_dbapp&view=project&layout=edit&controller=project&searchid=0&Itemid=" . $itemid . " >&nbsp;New&nbsp;sample&nbsp;</a>";

  $sorturlhead = "<a href=index.php?option=com_dbapp&view=projects&layout=default&Itemid="
                  . $itemid . "&clientId=" . urlencode($clientId) . "&cancelled=" . $cancelled
                  . "&strt=" . $strt
                  . "&managerId=" . urlencode($managerId) . "&contactId=" . urlencode($contactId) . "&sortKey=";
  $newsorter = ($sortKey == "newest")? "Newest first" : ($sorturlhead . "newest>Newest first</a>");
  $platesorter = ($sortKey == "plate")? "SampleId" : ($sorturlhead . "plate>SampleId</a>");
  $pisorter = ($sortKey == "pi")? "P.I." : ($sorturlhead . "pi>P.I.</a>");
  $statussorter = ($sortKey == "status")? "Status" : ($sorturlhead . "status>Status</a>");
  $nonasgnsorter = ($sortKey == "nonasgn")? "#Non-assigned" : ($sorturlhead . "nonasgn>#Non-assigned</a>");
  $managersorter = ($sortKey == "manager")? "Mngr" : ($sorturlhead . "manager>Mngr</a>");
  $contactsorter = ($sortKey == "contact")? "Contact" : ($sorturlhead . "contact>Contact</a>");

  $cancelchange = ($cancelled == "yes")? "Hide" : "Show";
  if ($cancelled == "yes")
    $cancelhead = str_replace("cancelled=yes", "cancelled=no", $sorturlhead);
  else 
    $cancelhead = str_replace("cancelled=no", "cancelled=yes", $sorturlhead);
  $cancelledfilter = $cancelhead . $sortKey . ">" . $cancelchange . " cancelled</a>";

  if ($strt == "yes") {
    $strtchange = "Only non-STRT";
    $strthead = str_replace("strt=yes", "strt=no", $sorturlhead);
  }
  else if ($strt == "no") {
    $strtchange = "Show STRT & non-STRT";
    $strthead = str_replace("strt=no", "strt=all", $sorturlhead);
  } else {
    $strtchange = "Only STRT";
    $strthead = str_replace("strt=all", "strt=yes", $sorturlhead);
  }
  $strtfilter = $strthead . $sortKey . ">" . $strtchange . "</a>";

  $filterText = "";
  if ($clientId != "") $filterText .= " for P.I. " . $clientId;
  if ($managerId != "") $filterText .= " for manager " . $managerId;
  if ($contactId != "") $filterText .= " for contact " . $contactId;
  if ($cancelled == "yes") $filterText .= " including cancelled";
  if ($strt != "all") $filterText .= " " . $strtchange;

  echo "<h1>Samples $filterText </h1>";
  echo "<div class='project'>
          <fieldset>
            <legend>
               <nobr> $cancelledfilter $strtfilter &nbsp;&nbsp; $newlink </nobr><br /><br />
               <nobr>Sort: $newsorter $platesorter $nonasgnsorter $pisorter
                           $contactsorter $managersorter $statussorter
               </nobr>
            </legend>
          <table><tr>
            <th colspan='2'></th>
            <th>SampleId&nbsp;<br />" . JHTML::tooltip('Plate L-number or other designation (e.g. S-number) for non-STRT plates/samples') . "</th>
            <th>Species&nbsp;<br />" . JHTML::tooltip('Only meaningful if no layout file is given') . "</th>
            <th>P.I.&nbsp;</th>
            <th>Contact&nbsp;</th>
            <th>Mngr&nbsp;<br />" . JHTML::tooltip('The manager receives email when analysis results are available') . "</th>
            <th>Plan&nbsp;<br />" . JHTML::tooltip('Total no. of lanes that are planned') . "</th>
            <th>Asgn&nbsp;<br />" . JHTML::tooltip('Total no. of lanes that have been assigned to runs') . "</th>
            <th>Status&nbsp;</th>
            <th>Res&nbsp;<br />" . JHTML::tooltip('Number of analysis setup') . "</th>
            <th>Layout&nbsp;<br />" . JHTML::tooltip('Click to view layout file') . "</th>";

    function newestsort($a, $b) { if ($a->id == $b->id) { return 0; }
                                  return ($a->id > $b->id) ? -1 : 1; };
    function plateidsort($a, $b) { if ($a->plateid == $b->plateid) { return 0; }
                                  return ($a->plateid > $b->plateid) ? -1 : 1; }; 
    function pisort($a, $b) { if ($a->principalinvestigator == $b->principalinvestigator) { return 0; }
                                  return ($a->principalinvestigator > $b->principalinvestigator) ? -1 : 1; }; 
    function managersort($a, $b) { if ($a->person == $b->person) { return 0; }
                                  return ($a->person > $b->person) ? -1 : 1; }; 
    function contactsort($a, $b) { if ($a->contactperson == $b->contactperson) { return 0; }
                                  return ($a->contactperson > $b->contactperson) ? -1 : 1; }; 
    function statussort($a, $b) { if ($a->astatus == $b->astatus)
                                  { return ($a->analysiscount > $b->analysiscount)? -1 : 1; }
                                  return (($a->astatus . "z") < ($b->astatus . "z")) ? -1 : 1; }; 
    function nonasgnsort($a, $b) { $an = $a->plannedlanes - $a->assignedlanes;
                                   $bn = $b->plannedlanes - $b->assignedlanes;
                                   if ($an == $bn) { return 0; }
                                   return ($an > $bn) ? -1 : 1; }; 
  if ($sortKey == "newest") {
    usort($this->projects, "newestsort");
  } else if ($sortKey == "plate") {
    usort($this->projects,"plateidsort");
  } else if ($sortKey == "pi") {
    usort($this->projects, "pisort");
  } else if ($sortKey == "manager") {
    usort($this->projects, "managersort");
  } else if ($sortKey == "contact") {
    usort($this->projects, "contactsort");
  } else if ($sortKey == "status") {
    usort($this->projects, "statussort");
  } else if ($sortKey == "nonasgn") {
    usort($this->projects, "nonasgnsort");
  }

  foreach ($this->projects as $project) {
    if ($clientId != "" && $project->principalinvestigator != $clientId) continue;
    if ($managerId != "" && $project->person != $managerId) continue;
    if ($contactId != "" && $project->contactperson != $contactId) continue;
    if ($cancelled != "yes" && $project->status == "cancelled") continue;
    if ($strt == "yes" && $project->plateid[0] != "L") continue;
    if ($strt == "no" && $project->plateid[0] == "L") continue;

    echo "<tr>";
    $projectlink = "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
           . $project->id . "&Itemid=" . $itemid . ">view</a>";
    $editlink = "<a href=index.php?option=com_dbapp&view=project&layout=edit&controller=project&searchid=" 
           . $project->id . "&Itemid=" . $itemid . ">edit</a>";
    echo "<td style='padding-right:2px;'>" . $projectlink . "</td><td>" . $editlink . "</td>";
    echo "<td>" . $project->plateid . "</td>";
    echo "<td>" . $project->species . "</td>";
    echo "<td><nobr>" . $project->principalinvestigator . "</nobr></td>";
    echo "<td><nobr>" . $project->contactperson . "</nobr></td>";

    $mngr = $project->person;
    if (strpos($mngr, ' ') >= 1)
      $mngr = implode(array_map(create_function('$a', 'return $a[0];'), explode(' ', $project->person)));
    else $mngr = substr($mngr, 0, 5);
    echo "<td><nobr>" . $mngr . "</nobr></td>";
    echo "<td><nobr>" . $project->plannedlanes . "</nobr></td>";
    echo "<td><nobr>" . $project->assignedlanes . "</nobr></td>";
    echo "<td><nobr>" . (($project->status == 'cancelled')? 'cancelled' : $project->astatus) . "</nobr></td>";
    echo "<td>" . (($project->analysiscount > 0)? $project->analysiscount : "-") . "</td>";
    $filelink = "";
    if ($project->layoutfile) $filelink = "yes";
    echo "<td><nobr><a href='/uploads/" . $project->layoutfile . "' target='_blank' >" . $filelink . "</a></nobr></td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />";

?>

