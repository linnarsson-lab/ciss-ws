<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid', -1) ;
  $mailtasks = $this->mailtasks;

  if ($searchid >= 0) {
    $db =& JFactory::getDBO();
    $query = " DELETE FROM #__aaafqmailqueue WHERE id=" . $db->Quote($searchid);
    $db->setQuery($query);
    if (! $db->query()) {
      JError::raiseWarning('Message', JText::_('Could not remove task!'));
	  $searchid = -1;
    }
  }
?>
<script type="text/javascript">
function confirmRemove() {
  return confirm("Really remove this mailing task?");
}
</script>
<h1>All FastQ mailing tasks</h1><br />
  <div><fieldset><legend></legend>
    <table><tr><th>Run&nbsp;</th><th>Lane&nbsp;</th><th>Recepient</th><th>Status</th><th></th></tr>
<?php
  foreach ($mailtasks as $task) {
    if ($task->id == $searchid) continue;
    $cancellink = "";
	if ($task->status == "inqueue")
        $cancellink = "<a href=\"index.php?option=com_dbapp&view=entry&layout=mailqueue&controller=entry&searchid=" 
                      . $task->id . "&Itemid=" . $itemid . "\" onclick=\"return confirmRemove();\">Remove</a>";
    echo "<tr><td>" . $task->runno . "</td><td>" . $task->laneno . "</td><td>" . $task->email 
	     . "&nbsp;</td><td>" . $task->status . "&nbsp;</td><td>" . $cancellink . "&nbsp;</td></tr>\n";
  }
  echo "</table></fieldset></div>";
  echo "<br />\n";
  echo "<a href=index.php?option=com_dbapp&view=entry&Itemid=" . $itemid . ">Return to pipeline overview</a>";
?>
