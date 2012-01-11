<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $seqbatch = $this->sequencingbatch;
  $editlink = "<a href=index.php?option=com_dbapp&view=sequencingbatch&layout=edit&controller=sequencingbatch&searchid=" 
           . $seqbatch->id . "&Itemid=" . $itemid . ">Edit this record</a>&nbsp;";
  $projlink = "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
           . $seqbatch->aaaprojectid . "&Itemid=" . $itemid . ">" . $seqbatch->plateid . "</a>";

  $removelink = "";
  if (count($this->illuminaruns) == 0) {
    $removelink = "<a href=index.php?option=com_dbapp&view=sequencingbatch&layout=remove&controller=sequencingbatch&searchid=" 
                  . $seqbatch->id . "&Itemid=" . $itemid
                  . " onclick=\"return confirm('This batch will be deleted.');\">Remove this record</a>&nbsp;";
  }

  $pi = ($seqbatch->principalinvestigator == "")? "Unknown" : $seqbatch->principalinvestigator;
  $cancelstatus = ($seqbatch->platestatus == "cancelled")? "cancelled" : "";
  echo "<h1>Sequencing batch $seqbatch->sr_title of $cancelstatus sample $projlink &nbsp; 
            P.I.: $pi</h1>
        <div class='sequencingbatch'>
        <fieldset>
          <legend> $editlink &nbsp; $removelink </legend>
          <table>
            <tr><td>Batch&nbsp;no:&nbsp;</td><td>" . $seqbatch->sr_title . "</td></tr>";
    //echo "<tr><td>Lab&nbsp;book (page):</td><td>" . $seqbatch->labbookpage . "</td></tr>";
    echo "<tr><td>Planned no of lanes:&nbsp;</td><td>" . $seqbatch->plannednumberoflanes . "</td></tr>";
    echo "<tr><td>Planned no of cycles:&nbsp;</td><td>" . $seqbatch->plannednumberofcycles . "</td></tr>";
    echo "<tr><td>Sequencing primer:&nbsp;</td><td>" . $seqbatch->primer . "</td></tr>";
    echo "<tr><td>Planned no of cycles:&nbsp;</td><td>" . $seqbatch->plannedindexcycles . "</td></tr>";
    echo "<tr><td>Index primer:&nbsp;</td><td>" . $seqbatch->indexprimer . "</td></tr>";
    echo "<tr><td>Cost:</td><td>" . $seqbatch->cost . "</td></tr>";
    echo "<tr><td>Signed:</td><td>" . $seqbatch->signed . "</td></tr>";
    echo "<tr><td>Invoice sent:</td><td>" . $seqbatch->invoice . "</td></tr>";
    echo "<tr><td>Comment:&nbsp;</td><td>" . $seqbatch->comment . "</td></tr>";
    echo "<tr><td>User:&nbsp;</td><td>" . $seqbatch->user . "</td></tr>";
    echo "<tr><td>Latest&nbsp;edit:&nbsp;</td><td>" . $seqbatch->time . "</td></tr></table></fieldset></div>";
    echo "<br />";

    echo "<div class='illuminarun'><fieldset><legend>Associated sequencing lanes</legend>";
if (count($this->illuminaruns) > 0) {
    echo "<table>
            <tr><th>RunNo&nbsp;</th>
                <th>RunId&nbsp;</th>
                <th>Status</th>
                <th>LaneNo&nbsp;</th>
                <th>Valid&nbsp;</th>
                <th>Comment&nbsp;</th>
            </tr>";
    foreach ($this->illuminaruns as $ills) {
      echo "<tr>
              <td><a href=index.php?option=com_dbapp&view=illuminarun&layout=illuminarun&controller=illuminarun&searchid=" . $ills->id . "&Itemid=" . $itemid . ">" . $ills->runnumber . "</a>&nbsp;</td>
              <td><a href=index.php?option=com_dbapp&view=illuminarun&layout=illuminarun&controller=illuminarun&searchid=" . $ills->id . "&Itemid=" . $itemid . ">" . $ills->RunNo . "</a>&nbsp;</td>
              <td>" . $ills->Rstatus . "&nbsp;</td>
              <td>" . $ills->laneno . "&nbsp;</td>
              <td>" . (($ills->Lstatus == "invalid")? "---" : "Yes") . "&nbsp;</td>
              <td>" . $ills->Lcomment . "&nbsp;</td>
            </tr>";
    }
    echo "</table><br />";

} else {
    echo "No Illumina runs defined<br />";
}
echo "</fieldset></div><br /><a href=index.php?option=com_dbapp&view=sequencingbatches&Itemid="
	 . $itemid . ">Return to sequencing batches list</a>";

?>
