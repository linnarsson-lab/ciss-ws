<?php
defined('_JEXEC') or die('Restricted access');

$searchid = JRequest::getVar('searchid') ;
$afteredit = $this->afteredit;
$submit = $afteredit['submit'];

if ($submit == 'Cancel') {
  JError::raiseNotice('Message', JText::_('Cancel - no actions! ' . $fileresult));
} else {
  $db =& JFactory::getDBO();
  $maxlaneidx = $afteredit['maxlaneidx'];
  $nlanes = 0;
  for ($laneidx = 1; $laneidx <= $maxlaneidx; $laneidx++) {
    if ($afteredit['lanesel' . $laneidx]) $nlanes++;
  }
  $projectid = $db->Quote($afteredit['projectid']);
  $build = $db->Quote($afteredit['build']);
  $variants = $db->Quote($afteredit['variants']);
  $rpkm = ($afteredit['rpkm'])? "1" : "0";
  $emails = $db->Quote($afteredit['emails']);
  $comment = $db->Quote($afteredit['comment']);
  $user =& JFactory::getUser();
  $username = $db->Quote($user->username);
  $query = " INSERT INTO #__aaaanalysis 
                (#__aaaprojectid, transcript_db_version, transcript_variant, rpkm, emails, status, lanecount, comment, time, user)
                VALUES ($projectid, $build, $variants, $rpkm, $emails, 'inqueue', $nlanes, $comment, NOW(), $username) ";
  $db->setQuery($query);
  if ($db->query()) {
    JError::raiseNotice('Message', JText::_('The analysis setup was saved!'));
    $analysisid = $db->Quote($db->insertid());
    $nlanes = 0;
    for ($laneidx = 1; $laneidx <= $maxlaneidx; $laneidx++) {
      if ($afteredit['lanesel' . $laneidx]) {
        $laneid = $db->Quote($afteredit['laneid' . $laneidx]);
        $lquery = " INSERT INTO #__aaaanalysislane (#__aaaanalysisid, #__aaalaneid) VALUES ($analysisid, $laneid) ";
        $db->setQuery($lquery);
        if ($db->query()) {
          $nlanes++;
        } else {
          JError::raiseWarning('Message', JText::_("Could not add lane [DBId: $laneid] to analysis!"));
        }
      }
    }
    JError::raiseNotice('Message', JText::_("Added $nlanes lanes to analysis."));
  } else {
    JError::raiseWarning('Message', JText::_('Could not save analysis setup! ' . $fileresult));
  }
  echo $db->getErrorMsg();
}
$menus = &JSite::getMenu();
$menu  = $menus->getActive();
$itemid = $menu->id;
echo "<br /><a href=index.php?option=com_dbapp&view=projects&Itemid="
     . $itemid . ">Return to samples list</a><br />&nbsp;<br />";
?>

