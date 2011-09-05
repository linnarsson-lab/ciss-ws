<?php // no direct access
defined('_JEXEC') or die('Restricted access'); ?>
<?php 
$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$itemid = $menu->id;
$searchid = JRequest::getVar('searchid', -1) ;

if ($searchid == -1) {
  JError::raiseWarning('Message', JText::_('Sequencing batch to remove (-1) does not exist!'));
} else if (count($this->illuminaruns) > 0) {
  JError::raiseWarning('Message', JText::_('A sequencing batch that has some runs defined can not be removed!'));
} else {
  $db =& JFactory::getDBO();
  $query = " DELETE FROM #__aaasequencingbatch WHERE id=" . $db->Quote($searchid);
  $db->setQuery($query);
  if (! $db->query()) {
    JError::raiseWarning('Message', JText::_('Could not remove sequencing batch!'));
  } else {
    echo "<h1>The sequencing batch has been removed.</h1>";
    JError::raiseNotice('Message', JText::_('Record ' . $searchid . ' was removed!'));
  }
}

echo "<br /><a href=index.php?option=com_dbapp&view=sequencingbatches&Itemid="
	 . $itemid . ">Return to sequencing batches list</a>";
?>
