<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $project = $this->project;
  
$removelink = "";
if (count($this->seqbatches) == 0 && $project->status != 'inqueue' && $project->status != 'processing') {
  $removelink = "<a href=\"index.php?option=com_dbapp&view=project&layout=remove&controller=project&searchid=" 
           . $project->id . "&Itemid=" . $itemid . "\" onclick=\"return confirm('This sample will be deleted.');\" title=\"Will irreversibly delete this sample from the database.\">Remove sample</a>&nbsp;";
}
$cancellink = ""; 
if ($project->status != 'cancelled') {
    $cancellink = "<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
	   . $project->id . "&cancel=yes&Itemid=" . $itemid . "\" title=\"Hide this (e.g. failed) sample from the sample list, but do not delete it from database.\">Cancel sample</a>";
} else {
    $cancellink = "<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
	   . $project->id . "&cancel=no&Itemid=" . $itemid . "\" title=\"Make this currently cancelled sample visible in the sample list.\">Reactivate sample</a>";
}
$editlink = "<a href=\"index.php?option=com_dbapp&view=project&layout=edit&controller=project&searchid=" 
         . $project->id . "&Itemid=" . $itemid . "\">Edit this record</a>";
 
   $cancelcmt = ($project->status == 'cancelled')? " cancelled": "";
    echo "<h1>View of$cancelcmt sample $project->plateid </h1>";
    echo "<div class='project'><fieldset>
            <legend> $editlink &nbsp; $cancellink &nbsp; $removelink </legend>";

    echo "<table><tr><td align=top>";
    echo "<fieldset><legend>Project data</legend>";
    echo "<table><tr><th>Title:&nbsp;</th><td>" . $project->title . "</td></tr>";
    echo "<tr><th>P.I.:&nbsp;</th><td>" . $project->principalinvestigator . "</td></tr>";
    echo "<tr><th>Contact:&nbsp;</th><td>" . $project->contactperson . "</td></tr>";
    echo "<tr><th>Manager:&nbsp;</th><td>" . $project->person . "</td></tr></table>";
    echo "</fieldset></td>";

    echo "<td align=top><fieldset><legend>Sample data</legend>";
    echo "<table><tr><th>Production date:&nbsp;</th><td>" . $project->platereference . "</td></tr>";
    echo "<tr><th>Species:&nbsp;</th><td>" . $project->species . "</td></tr>";
    echo "<tr><th>Tissue:</th><td>" . $project->tissue . "</td></tr>";
    echo "<tr><th>Sample&nbsp;type:</th><td>" . $project->sampletype . "</td></tr>";
    echo "<tr><th>Collection&nbsp;method:</th><td>" . $project->collectionmethod . "</td></tr></table>";
    echo "</fieldset></td>";
    echo "<td align=top>";

    if ($project->status == 'cancelled') {
      echo "<br /><br /> &nbsp; Project is cancelled <br /> ";
    } else if (count($this->seqbatches) == 0) {
      echo "&nbsp; (Define some batch before queueing)&nbsp;";
    } else { //if ($project->status == 'inqueue') {
      //  $projectlink = "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
      //                 . $project->id . "&inqueue=ready&Itemid=" . $itemid . ">RemoveFromQueue</a>&nbsp;";
      //  echo "&nbsp; To skip analysis click " . $projectlink . "</b> &nbsp; &nbsp; ";
      //} else if ($project->status != 'processing') {
      //  $projectlink = "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
      //                 . $project->id . "&inqueue=inqueue&Itemid=" . $itemid . ">PutInQueue</a>&nbsp;";
      echo "&nbsp; To setup an analysis click 'New analysis' below</b> &nbsp; &nbsp; ";
    }
    echo "</td></tr></table>";

    echo "<fieldset><legend>Sample setup</legend>";
    echo "<table><tr><td><table><tr><th>Concentration&nbsp;[ng/ul]:&nbsp;</th><td>" . $project->weightconcentration . "</td></tr>";
    echo "<tr><th>Fragment&nbsp;length:&nbsp;</th><td>" . $project->fragmentlength . "</td></tr>";
    echo "<tr><th>Concentration&nbsp;[nM]:&nbsp;</th><td>" . $project->molarconcentration . "  </td></tr>";
    echo "<tr><th>Spike&nbsp;mol.&nbsp;count&nbsp;</th><td>" . $project->spikemolecules . "</td></tr></table></td>";

    echo "<td><table><tr><th>Protocol:&nbsp;</th><td>" . $project->protocol . "</td></tr>";
    echo "<tr><th>Barcode&nbsp;set:&nbsp;</th><td>" . $project->barcodeset . "</td></tr>";

if ($project->fileupload == 1) {
    echo "<tr><th>Layout file:&nbsp;</th><td><a href='/uploads/" . $project->layoutfile . "' target='_blank' >" 
         . $project->layoutfile . "</a> [exists]</td></tr>";
} else {
    echo "<tr><th>Layout file:&nbsp;</th><td>" . $project->layoutfile . " [does not exist??]</td></tr>";
}
    echo "<tr><th>Comment:&nbsp;</th><td>" . $project->comment . "</td></tr></table></td></tr></table></fieldset></div>";

  $newbatchlink = ($project->status == 'cancelled')? "" :
                    "<a href=\"index.php?option=com_dbapp&view=sequencingbatch&layout=edit&controller=sequencingbatch&searchid=0&ProjectId=" 
                  . $project->id . "&BatchNo="
                  . (count($this->seqbatches) + 1) . "&Itemid=" . $itemid . "\">New sequencing batch</a>";
  echo "<div class='sequencingbatch'><fieldset><legend><nobr>Associated sequencing batches &nbsp; &nbsp; &nbsp;$newbatchlink</nobr></legend>";

if (count($this->seqbatches) != 0) {
  echo "<table>
          <tr>
            <th>SampleId&nbsp;</th>
            <th><nobr>Batch (DB&nbsp;Id)&nbsp;</nobr></th>
            <th>Planned lanes&nbsp;</th>
            <th>Assigned lanes&nbsp;</th>
            <th>IlluminaRuns&nbsp;</th>
            <th>Signed&nbsp;</th></tr>";
  foreach ($this->seqbatches as $batch) {
    $batchlink = "&nbsp;  <a href=\"index.php?option=com_dbapp&view=sequencingbatch&layout=sequencingbatch&controller=sequencingbatch&searchid=" 
                 . $batch->id . "&Itemid=" . $itemid . "\">" . $batch->batchno . " (" . $batch->id . ")</a>&nbsp;";
    echo "<tr>
            <td> $batch->plateid </td>
            <td> $batchlink </td>
            <td> $batch->plannednumberoflanes </td>
            <td> $batch->assignedlanes </td>";
    $illrow = preg_split('/,/', $batch->illids);
    if (count($illrow) == 0) {
      echo "<td> No Illumina runs defined </td>";
    } else if (count($illrow) > 0) {
        $illstring = "";
        foreach ($illrow as $ills) {
          $ill = preg_split('/-/', $ills);
          $runlink = " <a href=index.php?option=com_dbapp&view=illuminarun&layout=illuminarun&controller=illuminarun&searchid=" 
                     . $ill[0] . "&Itemid=" . $itemid . ">" . $ill[1] . "</a>&nbsp;";
          $illstring = $illstring . $runlink;
        }
        echo "<td> $illstring </td>";
    }

    echo "<td>" . $batch->signed . "</td>";
    echo "</tr>";
  }
  echo "</table>";
} else {
      echo " No batch yet defined <br />";
}
  echo "</fieldset></div>";

    echo "<fieldset>";
    echo "<b>User:&nbsp; </b>  " . $project->user . " &nbsp; &nbsp;<b>  Latest&nbsp;edit:&nbsp;</b>  " . $project->time . "  &nbsp; &nbsp;  &nbsp; &nbsp; ";
    echo "<a href=index.php?option=com_dbapp&view=projects&Itemid=" . $itemid . ">Return to sample list</a>";
    echo "</fieldset>";
    $newanalysislink = ($project->status == 'cancelled' || count($this->seqbatches) == 0)? "" :
                 "<a href=index.php?option=com_dbapp&view=lanes&layout=setupanalysis&controller=lanes&&projectid="
                 . $project->id . "&Itemid=" . $itemid . " >New analysis</a>";

    echo "<div class='analysis'><fieldset><legend>Analysis results $newanalysislink</legend>";
    if (count($this->analysis) != 0) {
      echo "<table>
              <tr>
                <th></th>
                <th></th>
                <th>DBId</th>
                <th>#L&nbsp;" . JHTML::tooltip('Number of lanes in analysis.') . "</th>
                <th>Status&nbsp;</th>
                <th>Extr&nbsp;" . JHTML::tooltip('Version of sequence filter and barcoded extraction software used for processing') . "&nbsp;</th>
                <th>Annot&nbsp;" . JHTML::tooltip('Version of feature annotation software used for processing') . "&nbsp;</th>
                <th>Gnm&nbsp;</th>
                <th>DBVer&nbsp;" . JHTML::tooltip('Source and creation date of genome and annotation database. Source may change after analysis if unavailable at processing time') . "&nbsp;</th>
                <th>Type&nbsp;" . JHTML::tooltip('all=known transcript variants analyzed separately, single=one value for each locus') . "&nbsp;</th>
                <th>Rpkm&nbsp;</th>
                <th>ResultsPath&nbsp;</th>
                <th>Rpt@</th>
                <th>Cmnt</th>
              </tr>";

      foreach ($this->analysis as $analys) {
        $analysissummary = "";
        $resultname = ($analys->status == 'ready')? "<b>Results file is missing!</b>" : "";
        if (file_exists($analys->resultspath)) {
          $analysissummary = "<a href=index.php?option=com_dbapp&view=project&controller=project&layout=analysis&searchid=" . $analys->id . "&Itemid=" . $itemid . ">view</a>&nbsp;";  
          $a = explode('/', $analys->resultspath);
          $resultname = $a[sizeof($a)-1];
        }
        else if ($analys->status == "failed") {
          $analysissummary = "<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
                             . $project->id . "&resretry=" . $analys->id . "&Itemid=" . $itemid
                             . "\" title=\"Try processing this analysis again.\" >retry</a>&nbsp;";
        }
        $cancelledstyle = "";
        $rescancellink = "";
        if ($analys->status != 'cancelled') {
            $rescancellink = "<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
                             . $project->id . "&rescancel=" . $analys->id . "&Itemid=" . $itemid
                             . "\" title=\"Invalidate this analysis result.\""
                             . " onclick=\"return confirm('This analysis will be cancelled but still visible under the project.');\" >cancel</a>&nbsp;";
        } else {
            $cancelledstyle = " style=\"font-style:italic; color:grey;\"";
         //   $rescancellink = "<a href=\"index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid="
         //                    . $project->id . "&resrescue=" . $analys->id . "&Itemid=" . $itemid
         //                    . "\" title=\"Make this cancelled analysis result valid again.\">rescue</a>&nbsp;";
        }
        echo "<tr$cancelledstyle><td>$analysissummary</td>";
        echo "<td>$rescancellink</td>";
        echo "<td>" . $analys->id . "&nbsp;</td>";
        echo "<td>" . $analys->lanecount . "&nbsp;</td>";
        echo "<td>" . $analys->status . "&nbsp;</td>";
        echo "<td>" . $analys->extraction_version . "&nbsp;</td>";
        echo "<td>" . $analys->annotation_version . "&nbsp;</td>";
        echo "<td>" . $analys->genome . "&nbsp;</td>";
        $dbver = $analys->transcript_db_version;
        if (preg_match('/^(.+)_[0-9]+bp([0-9]+)/', $dbver, $m)) {
          $dbver = $m[1] . $m[2];
        }
        echo "<td>" . $dbver . "&nbsp;</td>";
        echo "<td>" . $analys->transcript_variant . "&nbsp;</td>";
        echo "<td>" . (($analys->rpkm == "1")? "Yes" : "---") . "&nbsp;</td>";
        echo "<td>" . $resultname . "</td>";
        if (strlen($analys->emails) > 2)
            echo "<td>&nbsp;" . JHTML::tooltip($analys->emails) . "</td>";
        else
            echo "<td>" . $analys->emails . "</td>";
        $comment = $analys->comment;
        if (strlen($comment) > 8)
            echo "<td>&nbsp;" . JHTML::tooltip($comment) . "</td></tr>";
        else
            echo "<td>&nbsp;" . $comment . "</td></tr>";
      }
      echo "</table>";
    } else {
      echo "  Not yet run <br />";
    }
    echo "<br /></fieldset></div>";
?>
