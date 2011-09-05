<?php
defined('_JEXEC') or die('Restricted access');
?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $newClink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;client&nbsp;</a>";
  $newPlink = "<a href=index.php?option=com_dbapp&view=project&layout=edit&controller=project&searchid=0&Itemid=" . $itemid . " >&nbsp;Add&nbsp;new&nbsp;project&nbsp;</a>";

  echo "<H1>Illumina Sequencing Database</H1>";
  echo "<p>This is the database handling our sequencing runs. All sequencing projects should have a responsible Principal Investigator. Make sure that the P.I. exists before you continue. Moreover, each project should have a responsible person running the experiment. This person is the Manager. The client (P.I.) also has an appointed contact person to the project. Client, Manager and Contact should be defined before you define the Project.</p>";

  echo "<div class='client'><fieldset><legend>List of clients</legend><table>";
  echo "<tr><th>View</th><th>Edit</th><th>$newClink</th><th></th></tr>";
  echo "<tr><th>&nbsp;Principal&nbsp;Investigator&nbsp;</th>
            <th>&nbsp;Department&nbsp;</th>
            <th>&nbsp;Category&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>";
  foreach ($this->clients as $client) {
    echo "<tr>";
    $clientlink = "<a href=index.php?option=com_dbapp&view=client&layout=client&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">";
    echo "<td>" . $clientlink . $client->principalinvestigator . "</a></td>";
    echo "<td>" . $editlink . $client->department . "</a></td>";
    echo "<td>" . $client->category . "</td>";
    echo "<td>" . $client->user . " " ;
    echo $client->time . "</td>";
    echo "<tr>";
  }
  echo "</table></fieldset></div><br />";

  echo "<div class='project'><fieldset><legend>List of Projects</legend><table>";
  echo "<tr><th>View</th><th>Edit</th><th></th><th>$newPlink</th><th></th><th></th><th></th></tr>";
  echo "<tr><th>&nbsp;Plate&nbsp;id&nbsp;</th>
            <th>&nbsp;Species&nbsp;</th>
            <th>&nbsp;P.&nbsp;I.&nbsp;</th>
            <th>&nbsp;Contact&nbsp;person&nbsp;</th>
            <th>&nbsp;Manager&nbsp;</th>
            <th>&nbsp;Tissue&nbsp;</th>
            <th>&nbsp;Latest&nbsp;edit&nbsp;</th></tr>";
  foreach ($this->projects as $project) {
    echo "<tr>";
    $projectlink = "<a href=index.php?option=com_dbapp&view=project&layout=project&controller=project&searchid=" 
           . $project->id . "&Itemid=" . $itemid . ">";
    $editlink = "<a href=index.php?option=com_dbapp&view=project&layout=edit&controller=project&searchid=" 
           . $project->id . "&Itemid=" . $itemid . ">";
    echo "<td>" . $projectlink . $project->plateid . "</a></td>";
    echo "<td>" . $editlink . $project->species . "</a></td>";
    echo "<td>" . $project->principalinvestigator . "</td>";
    echo "<td>" . $project->contactperson . "</td>";
    echo "<td>" . $project->person . "</td>";
    echo "<td>" . $project->tissue . "</td>";
    echo "<td>" . $project->user . " " ;
    echo $project->time . "</td>";
    echo "</tr>";
  }
  echo "</table></fieldset></div><br />";
  echo "<p>This is the main entry point for the database. You can list Projects, Clients, Managers, Contacts, 'Sequencing batches' and 'Illumina runs' using the menu items to the right. From these lists you also have the possibility to view and/or edit individual records. When the Project is defined (including client, manager and contact) you can add a sequencing run. The sequencing run is what the P.I. has to sign and the record include among other things 'planned number of runs'. When sequencing runs have been included you can include them in an Illumina run. First you define run i.e. add run date and any comments you might have. Then you start to add the 'sequencing runs' to the different lanes by 'edit lanes'. </p>";


?>
