<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid') ;
  $buptasks = $this->buptasks;

  echo "<h1>All backup tasks</h1><br />";
  echo "<div><fieldset><legend></legend>";
  echo "<table><tr><th>Path&nbsp;</th><th>Priority</th><th>Status</th><th></th></tr>";
  foreach ($buptasks as $task) {
    $cancellink = "";
	if ($task->status == "inqueue")
        $cancellink = "<a href=index.php?option=com_dbapp&view=entry&layout=cancelbup&controller=entry&searchid=" 
                      . $task->id . "&Itemid=" . $itemid . ">Cancel</a>";
    echo "<tr><td>" . $task->path . "&nbsp;</td><td>&nbsp;" . $task->priority
         . "</td><td>" . $task->status . "&nbsp;</td><td>" . $cancellink . "&nbsp;</td></tr>";
  }
  echo "</table></fieldset></div>";
  echo "<br />";
  echo "<a href=index.php?option=com_dbapp&view=entry&Itemid=" . $itemid . ">Return to pipeline overview</a>";
?>
