<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $mailtasks = $this->mailtasks;

  echo "<h1>All FastQ mailing tasks</h1><br />";
  echo "<div><fieldset><legend></legend>";
  echo "<table><tr><th>Run&nbsp;</th><th>Lane&nbsp;</th><th>Recepient</th><th>Status</th><th></th></tr>";
  foreach ($mailtasks as $task) {
    $cancellink = "";
	if ($task->status == "inqueue")
        $cancellink = "<a href=index.php?option=com_dbapp&view=entry&layout=cancelmail&controller=entry&searchid=" 
                      . $task->id . "&Itemid=" . $itemid . ">Cancel</a>";
    echo "<tr><td>" . $task->runno . "</td><td>" . $task->laneno . "</td><td>" . $task->email 
	     . "&nbsp;</td><td>" . $task->status . "&nbsp;</td><td>" . $cancellink . "&nbsp;</td></tr>";
  }
  echo "</table></fieldset></div>";
  echo "<br />";
  echo "<a href=index.php?option=com_dbapp&view=entry&Itemid=" . $itemid . ">Return to pipeline overview</a>";
?>
