<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $afteredit = $this->afteredit;
  $ae_keys = array_keys($afteredit);


    $db =& JFactory::getDBO();

  if ($searchid == 0) {
    $newquery = " INSERT INTO #__aaamanager ( ";
    $columns = "";
    $vcolumn = "";

    $query = " UPDATE #__aaamanager SET ";
    
  echo "<table>";
  foreach ($afteredit as $key => $value) {
    echo "<tr><td>" . $key . "</td><td>" . $value . "</td></tr>";
    if (($key == 'person') || ($key == 'email') || ($key == 'phone') || ($key == 'comment') || ($key == 'user') || ($key == 'time')) {
      $columns .= $key . ", ";
      $vcolumn .= $db->Quote($value) . ", ";
      $query .= $key . " = " . $db->Quote($value) . ", ";
    }
    if ($key == 'id') {
      $searchid = $value;
    }
    if ($key == 'submittype') {
      $submit = $value;
    }
  }
  echo "</table>";

//  echo "<H1>" . $afteredit->principalinvestigator . " &nbsp; &nbsp; &nbsp; &nbsp; id:" . $afteredit->id . "</H1><BR />";
    $query .= " hits = '1' WHERE id = '" . $searchid . "' ";
    $newquery .= $columns .  " hits) VALUES ( " . $vcolumn . " '1' ) "; 
    

     if ($searchid == 0) {
       $db->setQuery($newquery);
     } else {
       $db->setQuery($query);
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
    }
//    $aearr = array($afteredit);
  //  $db->updateObject('#__aaaclient', &$aearr, 'id');
    echo $db->getErrorMsg();

    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<br /><a href=index.php?option=com_dbapp&view=managers&Itemid=" . $itemid . ">Return to managers list</a><br />&nbsp;<br />";
?>


