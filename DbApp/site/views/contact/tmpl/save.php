<?php
defined('_JEXEC') or die('Restricted access');
//JHtml::_('behavior.tooltip');
//JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $afteredit = $this->afteredit;
  $ae_keys = array_keys($afteredit);
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $db =& JFactory::getDBO();

  if ($searchid == 0) {
    $newquery = " INSERT INTO #__aaacontact ( ";
    $columns = "";
    $vcolumn = "";
    $query = " UPDATE #__aaacontact SET ";
    
    echo "<table>\n";
    foreach ($afteredit as $key => $value) {
      echo "<tr><td>" . $key . "</td><td>" . $value . "</td></tr>\n";
      if ( ($key == 'contactperson') || ($key == 'contactemail') || ($key == 'contactphone')
           || ($key == 'comment') || ($key == 'user') || ($key == 'time') ) {
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
    echo "</table>\n";

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
      } else {
        JError::raiseWarning('Message', JText::_('Could not save record!'));
      }
    } else {
      JError::raiseWarning('Message', JText::_('Cancel - no actions!'));
    }
  }
  echo $db->getErrorMsg();

  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  echo "<br />\n" .
       "  <a href=\"index.php?option=com_dbapp&view=contacts&Itemid=$itemid\">Return to contacts list</a>" .
       "<br />&nbsp;<br />\n";
?>


