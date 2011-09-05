<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $client = $this->client;
  $editlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">Edit this record</a>";
    echo "<H1> Client - single record VIEW</H1><BR />";
    echo "<div class='client'><fieldset><legend> P.I. &nbsp; '" . $client->principalinvestigator . "' &nbsp; &nbsp; &nbsp; DB id: '" . $client->id . "' &nbsp; &nbsp; &nbsp; " . $editlink . "</legend>";
    echo "<table><tr><th>Department:&nbsp;</th><td>" . $client->department . "</td></tr>";
    echo "<tr><th>Category:</th><td>" . $client->category . "</td></tr>";
    echo "<tr><th>Address:</th><td>" . $client->address . "</td></tr>";
    echo "<tr><th>Vat&nbsp;No:</th><td>" . $client->vatno . "</td></tr>";
    echo "<tr><th>Comment:</th><td>" . $client->comment . "</td></tr>";
    echo "<tr><th>User:&nbsp;</th><td>" . $client->user . "";
    echo "<tr><th>Latest&nbsp;edit:&nbsp;</th><td>" . $client->time . "</td></tr></table></fieldset></div>";
    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&clientId=" . urlencode( $client->principalinvestigator ) . ">View sequencing batches</a>";
    echo "<br />";
    echo "<a href=index.php?option=com_dbapp&view=clients&Itemid=" . $itemid . ">Return to list</a>";
?>
