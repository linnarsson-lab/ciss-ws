<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
$searchid = JRequest::getVar('searchid') ;
$afteredit = $this->afteredit;
$ae_keys = array_keys($afteredit);

foreach ($this->illuminaruns as $lane) {
  $irilluminarunid = $lane->illuminarunid;
  $irdbid = $lane->id;
  $irrundate = $lane->rundate;
  $ircomment = $lane->comment;
  $iruser = $lane->user;
  $irtime = $lane->time;
}

$query = array();
$db =& JFactory::getDBO();

for ($i = 1; $i <= 8; $i++) {
  $query[$i] = " UPDATE #__aaalane SET ";
  $batchid = $afteredit['#__aaasequencingbatchid' . $i];
  $batchid = ($batchid == "X")? "NULL" : $db->Quote($batchid);
  $query[$i] .= " #__aaasequencingbatchid = " . $batchid . ", ";
  $query[$i] .= " molarconcentration  = " . $db->Quote($afteredit['molarconcentration' . $i]) . ", ";
  $query[$i] .= " yield  = " . $db->Quote($afteredit['yield' . $i]) . ", ";
  $query[$i] .= " comment  = " . $db->Quote($afteredit['comment' . $i]) . ", ";
  $query[$i] .= " user  = " . $db->Quote($afteredit['user' . $i]) . ", ";
  $query[$i] .= " time  = " . $db->Quote($afteredit['time' . $i]) . " ";
  $query[$i] .= " WHERE id  = " . $db->Quote($afteredit['id' . $i]) . " ";
}

$app =& JFactory::getApplication();
$counter = 0;

if ($afteredit['Submit'] != 'Save') {
  JError::raiseWarning('Message', JText::_('Cancel - no actions!'));
} else {
  for ($i = 1; $i <= 8; $i++) {
    $db->setQuery($query[$i]);
    if ($db->query()) {
      $counter++;
    } else {
      JError::raiseWarning('Message', JText::_("Could not save record for lane $i!"));
    }
  }
}

$app->enqueueMessage($counter . ' records updated!');
echo $db->getErrorMsg();

$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$itemid = $menu->id;
echo "<br /><a href=index.php?option=com_dbapp&view=illuminaruns&Itemid="
     . $itemid . ">Return to Illumina runs list</a><br />&nbsp;<br />";
?>


