<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $client = $this->client;
  $editlink = "<a href=index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=" 
           . $client->id . "&Itemid=" . $itemid . ">Edit this client</a>";
  echo "<h1>View of client $client->principalinvestigator</h1>\n";
  echo "<div class='client'><fieldset><legend>$editlink</legend>\n";
  echo "<table>\n"; 
  echo "  <tr><th>Department:&nbsp;</th><td>" . $client->department . "</td></tr>\n";
  echo "  <tr><th>Category:</th><td>" . $client->category . "</td></tr>\n";
  echo "  <tr><th>Address:</th><td>" . $client->address . "</td></tr>\n";
  echo "  <tr><th>Vat&nbsp;No:</th><td>" . $client->vatno . "</td></tr>\n";
  echo "  <tr><th>Comment:</th><td>" . $client->comment . "</td></tr>\n";
  echo "  <tr><th>Latest&nbsp;edit:&nbsp;</th><td>" . $client->user . "&nbsp;&nbsp;" . $client->time . "</td></tr>\n";
  echo "</table></fieldset></div>\n";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=projects&layout=default&clientId=" . urlencode( $client->principalinvestigator ) . ">View client's samples</a><br />\n";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=sequencingbatches&layout=default&clientId=" . urlencode( $client->principalinvestigator ) . ">View client's sequencing batches</a><br />\n";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=clients&Itemid=" . $itemid . ">Return to client list</a>\n";
?>

