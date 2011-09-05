<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewContacts extends JView {

	function display($tpl = null) {
                // Assign data to the view
    $contacts = $this->get('Contacts');
    $this->assignRef('contacts', $contacts);

    if (count($errors = $this->get('Errors'))) {
      JError::raiseError(500, implode('<br />', $errors));
      return false;
    }
 
    parent::display($tpl);

	}


}
