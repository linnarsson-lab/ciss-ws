<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$itemid = $menu->id;
$searchid = JRequest::getVar('searchid', -1) ;

if ($searchid == -1) {
  JError::raiseWarning('Message', JText::_('Sample to remove (-1) does not exist!'));
} else if ($project->status == 'inqueue' || $project->status == 'processing') {
  JError::raiseWarning('Message', JText::_('Samples that are in queue or processing can not be removed!'));
} else if (count($this->seqbatches) > 0) {
  JError::raiseWarning('Message', JText::_('Samples that have batches defined can not be removed! First remove batches.'));
} else {
  $db =& JFactory::getDBO();
  $query = "DELETE FROM #__aaaproject WHERE id=" . $db->Quote($searchid);
  $db->setQuery($query);
  if (! $db->query()) {
    JError::raiseWarning('Message', JText::_('Could not remove sample!'));
  } else {
    echo "<h1>The sequencing batch has been removed.</h1>";
    JError::raiseNotice('Message', JText::_('Record ' . $searchid . ' was removed!'));
    if (count($this->analysis) != 0)
      JError::raiseNotice('Message', JText::_('The sample had some analysis results. They will remain on the server.'));
  }
}

echo "<br /><a href=index.php?option=com_dbapp&view=projects&Itemid="
	 . $itemid . ">Return to sample list</a>";
?>
