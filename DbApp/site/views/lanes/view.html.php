<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewLanes extends JView {

  function display($tpl = null) {
    // Assign data to the view
    $lanes = $this->get('Items');
    $this->assignRef('lanes', $lanes);

    $afteredit = JRequest::get('POST');
    $this->assignRef('afteredit', $afteredit);

    if (count($errors = $this->get('Errors'))) {
      JError::raiseError(500, implode('<br />', $errors));
      return false;
    }
    parent::display($tpl);
  }

}
