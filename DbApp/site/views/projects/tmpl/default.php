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

  $sorturlhead = "<a href=index.php?option=com_dbapp&view=projects&layout=default&clientId=" . urlencode($clientId)
                  . "&cancelled=" . $cancelled . "&strt=" . $strt
                  . "&managerId=" . urlencode($managerId) . "&contactId=" . urlencode($contactId) . "&sortKey=";
  $newsorter = ($sortKey == "newest")? "Newest first" : ($sorturlhead . "newest>Newest first</a>");
  $platesorter = ($sortKey == "plate")? "SampleId" : ($sorturlhead . "plate>SampleId</a>");
  $pisorter = ($sortKey == "pi")? "P.I." : ($sorturlhead . "pi>P.I.</a>");
  $statussorter = ($sortKey == "status")? "Status" : ($sorturlhead . "status>Status</a>");
  $nonasgnsorter = ($sortKey == "nonasgn")? "#Non-assigned" : ($sorturlhead . "nonasgn>#Non-assigned</a>");
  $managersorter = ($sortKey == "manager")? "Mngr" : ($sorturlhead . "manager>Mngr</a>");
  $contactsorter = ($sortKey == "contact")? "Contact" : ($sorturlhead . "contact>Contact</a>");
  $bcsetsorter = ($sortKey == "bcset")? "BcSet" : ($sorturlhead . "bcset>BcSet</a>");
  $speciessorter = ($sortKey == "species")? "Spc" : ($sorturlhead . "species>Spc</a>");

  $cancelchange = ($cancelled == "yes")? "Hide" : "Show";
  if ($cancelled == "yes")
    $cancelhead = str_replace("cancelled=yes", "cancelled=no", $sorturlhead);
  else 
    $cancelhead = str_replace("cancelled=no", "cancelled=yes", $sorturlhead);
  $cancelledfilter = $cancelhead . $sortKey . ">" . $cancelchange . " cancelled</a>";

  if ($strt == "yes") {
    $strtnow = "only STRT";
    $strtchange = "Only non-STRT";
    $strthead = str_replace("strt=yes", "strt=no", $sorturlhead);
  }
  else if ($strt == "no") {
    $strtnow = " non-STRT";
    $strtchange = "Show STRT & non-STRT";
    $strthead = str_replace("strt=no", "strt=all", $sorturlhead);
  } else {
    $strtnow = "";
    $strtchange = "Only STRT";
    $strthead = str_replace("strt=all", "strt=yes", $sorturlhead);
  }
  $strtfilter = $strthead . $sortKey . ">" . $strtchange . "</a>";

  $filterText = "";
  if ($clientId != "") $filterText .= " for P.I. " . $clientId;
  if ($managerId != "") $filterText .= " for manager " . $managerId;
  if ($contactId != "") $filterText .= " for contact " . $contactId;
  if ($cancelled == "yes") $filterText .= " including cancelled";
  if ($strt != "all") $filterText .= " " . $strtnow;
?>

<script type="text/javascript">
  function doFreeSearch(searchField) {
    var ss = searchField.value;
    if (ss.length < 3)
      ss = "";
    var i = 1;
    var searchelem = document.getElementById("row" + i + "search");
    while (searchelem != null) {
      var trelem = document.getElementById("row" + i);
      if (ss == "" || searchelem.innerHTML.search(ss) >= 0) {
        trelem.style.display = "table-row";
      } else {
            trelem.style.display = "none";
      }
      i = i + 1;
      searchelem = document.getElementById("row" + i + "search");
    }
  }
</script>

<?php
  echo "<h1>Samples $filterText </h1>\n";
  echo "<div class='project'>
          <fieldset>
            <legend>
               <nobr> $cancelledfilter $strtfilter &nbsp;&nbsp; $newlink </nobr><br /><br />
               <nobr>Sort: $newsorter $platesorter $nonasgnsorter $bcsetsorter $speciessorter $pisorter
                           $contactsorter $managersorter $statussorter
                           &nbsp;&nbsp;&nbsp;
                           Filter: <input type=\"text\" id=\"freeSearch\" onkeyup=\"return doFreeSearch(this);\" />
               </nobr>
            </legend>
          <table><tr>
            <th></th>
            <th>$platesorter&nbsp;<br />" . JHTML::tooltip('Plate L-number or other designation (e.g. S-number) for non-STRT plates/samples') . "</th>
            <th>$bcsetsorter&nbsp;<br />" . JHTML::tooltip('v4=48best wells w mol.counting, v4r=48best wells w/o mol.counting, v2=6-mer STRTver3, v1=5-mer STRTver1 no=single sample, TruSeq') . "</th>
            <th>$speciessorter&nbsp;<br />" . JHTML::tooltip('Only meaningful if no layout file is given') . "</th>
            <th>$pisorter&nbsp;</th>
            <th>$contactsorter&nbsp;</th>
            <th>$managersorter&nbsp;<br />" . JHTML::tooltip('The manager receives email when analysis results are available') . "</th>
            <th>Plan&nbsp;<br />" . JHTML::tooltip('Total no. of lanes that are planned') . "</th>
            <th>Asgn&nbsp;<br />" . JHTML::tooltip('Total no. of lanes that have been assigned to runs') . "</th>
            <th>$statussorter&nbsp;<br />" . JHTML::tooltip('--- is shown if last analysis was cancelled') . "</th>
            <th>Res&nbsp;<br />" . JHTML::tooltip('Number of analysis setup') . "</th>
            <th>Layout&nbsp;<br />" . JHTML::tooltip('Click to view layout file') . "</th>\n";

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
    function bcsetsort($a, $b) { return -strnatcasecmp($a->barcodeset, $b->barcodeset); }
    function speciessort($a, $b) { return -strnatcasecmp($a->species, $b->species); }
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
  } else if ($sortKey == "bcset") {
    usort($this->projects, "bcsetsort");
  } else if ($sortKey == "species") {
    usort($this->projects, "speciessort");
  }

  $rownum = 1;
  foreach ($this->projects as $project) {
    if ($clientId != "" && $project->principalinvestigator != $clientId) continue;
    if ($managerId != "" && $project->person != $managerId) continue;
    if ($contactId != "" && $project->contactperson != $contactId) continue;
    if ($cancelled != "yes" && $project->status == "cancelled") continue;
    if ($strt == "yes" && $project->plateid[0] != "L") continue;
    if ($strt == "no" && $project->plateid[0] == "L") continue;

    echo "<tr id=\"row" . $rownum . "\">\n";
    echo "<td id=\"row" . $rownum . "search\" style=\"display:none;\">" . $project->rowsearch . ">\n";
    $rownum = $rownum + 1;
    $projectlink = "<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
                   . $project->id . "&Itemid=$itemid\" title=\"" . $project->title . " " . $project->comment . "\">"
                   . $project->plateid . "</a>";
    $editlink = "<a href=\"index.php?option=com_dbapp&view=project&layout=edit&controller=project&searchid=" 
                . $project->id . "&Itemid=" . $itemid . "\">edit</a>";
    echo "<td><nobr>&nbsp; $editlink &nbsp;</nobr></td>\n";
    echo "<td><nobr>&nbsp; $projectlink &nbsp;</nobr></td>\n";
    echo "<td>" . $project->barcodeset . "&nbsp;</td>\n";
    echo "<td>" . substr($project->species, 0, 3) . "&nbsp;</td>\n";
    $pilink = "<a href=\"index.php?option=com_dbapp&view=client&layout=client&controller=client&searchid="
              . $project->clientid . "&Itemid=" . $itemid . "\">$project->principalinvestigator</a>";
    echo "<td><nobr>&nbsp; $pilink &nbsp;</nobr></td>\n";
    $clientlink = "<a href=\"index.php?option=com_dbapp&view=contact&layout=contact&controller=contact&searchid="
                  . $project->contactid . "&Itemid=" . $itemid . "\">$project->contactperson</a>";
    echo "<td><nobr>&nbsp; $clientlink &nbsp;</nobr></td>\n";

    $mngr = $project->person;
    if (strpos($mngr, ' ') >= 1)
      $mngr = implode(array_map(create_function('$a', 'return $a[0];'), explode(' ', $project->person)));
    else $mngr = substr($mngr, 0, 5);
    echo "<td><nobr><abbr title=\"$project->person\">" . $mngr . "</abbr>&nbsp;</nobr></td>\n";
    echo "<td><nobr>" . $project->plannedlanes . "</nobr></td>\n";
    echo "<td><nobr>" . $project->assignedlanes . "</nobr></td>\n";
    if ($project->status == 'cancelled')
         echo "<td><nobr>cancelled&nbsp;</nobr></td>\n";
    else
        echo "<td><nobr>" . (($project->astatus == 'cancelled')? '---' : $project->astatus) . "&nbsp;</nobr></td>\n";
    echo "<td>&nbsp;" . (($project->analysiscount > 0)? $project->analysiscount : "-") . "&nbsp;</td>\n";
    $filelink = "";
    if ($project->layoutfile) $filelink = "yes";
    echo "<td><nobr><a href='/uploads/" . $project->layoutfile . "' target='_blank' >" . $filelink . "</a></nobr></td>\n";
    echo "</tr>\n";
  }
  echo "</table></fieldset></div><br />\n";

?>
