<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $afteredit = $this->afteredit;
  $ae_keys = array_keys($afteredit);

    $db =& JFactory::getDBO();

//  if ($searchid == 0) {
    $newquery = " INSERT INTO #__aaalane ( ";
    $columns = "";
    $vcolumn = "";

    $query = " UPDATE #__aaalane SET ";
    
  echo "<table>";
  foreach ($afteredit as $key => $value) {
    echo "<tr><td>" . $key . "</td><td>" . $value . "</td></tr>";
    if (($key == 'laneno') || ($key == 'cycles') || ($key == 'molarconcentration') || ($key == 'yield') || ($key == 'comment') || ($key == 'user') || ($key == 'time') || ($key == '#__aaailluminarunid') || ($key == '#__aaasequencingbatchid')) {
      $columns .= $key . ", ";
      $vcolumn .= $db->Quote($value) . ", ";
      $query .= $key . " = " . $db->Quote($value) . ", ";
    }
    if ($key == 'id') {
      $searchid = $value;
    }
    if ($key == 'Submit') {
      $submit = $value;
    }
  }
  echo "</table>";

//  echo "<H1>" . $afteredit->principalinvestigator . " &nbsp; &nbsp; &nbsp; &nbsp; id:" . $afteredit->id . "</H1><BR />";
    $query .= " hits = '1' WHERE id = '" . $searchid . "' ";
    $newquery .= $columns .  " hits) VALUES ( " . $vcolumn . " '1' ) "; 
    

     if ($searchid == 0) {
       $db->setQuery($newquery);
//echo 'new' . $newquery;
     } else {
       $db->setQuery($query);
//echo 'updating' . $query;
     }

       if ($submit == 'Save') {
         if ($db->query()) {
           JError::raiseWarning('Message', JText::_('The record was saved!'));
//          echo JText::_('<br/>query ok<br/>');
         } else {
           JError::raiseWarning('Message', JText::_('Could not save record!'));
         }
       } else {
         JError::raiseWarning('Message', JText::_('Cancel - no actions!'));
       }
//    }
//    $aearr = array($afteredit);
  //  $db->updateObject('#__aaaclient', &$aearr, 'id');
    echo $db->getErrorMsg();

    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<br /><a href=index.php?option=com_dbapp&view=lanes&Itemid=" . $itemid . ">Return to list of lanes</a><br />&nbsp;<br />";
?>


