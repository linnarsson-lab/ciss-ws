<?php
defined('_JEXEC') or die('Restricted access');
$searchid = JRequest::getVar('searchid') ;
$afteredit = $this->afteredit;

$runno = $afteredit['runno'];

$db =& JFactory::getDBO();
$query = " INSERT INTO #__aaafqmailqueue (runno, laneno, email, status) VALUES ";
$qdata = "";
for ($i = 1; $i <= 8; $i++) {
  if ($afteredit["lanesel$i"]) {
    if ($qdata != "") $qdata .= ", ";
    $qdata .= "(" . $db->Quote($runno) . ", $i, " . $db->Quote($afteredit["email$i"]) . ", 'inqueue')";
  }
}
$query .= $qdata;

$app =& JFactory::getApplication();

if ($afteredit['Submit'] == 'Cancel') {
  JError::raiseWarning('Message', JText::_('Cancel - no actions!'));
} else {
  $db->setQuery($query);
  if ($db->query()) {
    $app->enqueueMessage('Fastq data added to mail queue.');
  } else {
    JError::raiseWarning('Message', JText::_("Could not queue up fastq data to email!"));
  }
}

echo $db->getErrorMsg();

$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$itemid = $menu->id;
echo "<br /><a href=index.php?option=com_dbapp&view=illuminaruns&Itemid="
     . $itemid . ">Return to Illumina runs list</a><br />&nbsp;<br />";
?>


