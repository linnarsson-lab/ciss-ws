<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $manager = $this->manager;
  $editlink = "<a href=index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=" 
           . $manager->id . "&Itemid=" . $itemid . ">Edit this manager</a>";
  echo "<h1>View of manager $manager->person</h1>\n";
  echo "<div class='manager'><fieldset><legend>$editlink</legend>\n";
  echo "<p> The manager is the person running the sequencing reactions/handling the Illumina machine </p>\n";
  echo "<table>\n";
  echo "  <tr><th>Email:&nbsp;</th><td>" . $manager->email . "</td></tr>\n";
  echo "  <tr><th>Phone&nbsp;no:&nbsp;</th><td>" . $manager->phone . "</td></tr>\n";
  echo "  <tr><th>Comment:&nbsp;</th><td>" . $manager->comment . "</td></tr>\n";
  echo "  <tr><th>Latest&nbsp;edit:&nbsp;</th><td> $manager->user &nbsp;&nbsp; $manager->time </td></tr>\n";
  echo "</table></fieldset></div>\n";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=projects&layout=default&managerId=" . urlencode( $manager->person ) . ">View manager's samples</a><br />\n";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=managers&Itemid=" . $itemid . ">Return to manager list</a>\n";
?>

