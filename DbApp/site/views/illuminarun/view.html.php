<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewIlluminaRun extends JView {

	// Overwriting JView display method
	function display($tpl = null) {
	  // Assign data to the view
      $items = $this->get('Items');
      $this->assignRef('illuminaruns', $items);

      $item = $this->get('Item');
      $this->assignRef('illuminarun', $item);

      $afteredit = JRequest::get('POST');
      $this->assignRef('afteredit', $afteredit);

	  // Check for errors.
	  if (count($errors = $this->get('Errors'))) {
		JError::raiseError(500, implode('<br />', $errors));
		return false;
	  }
	  // Display the view
	  parent::display($tpl);
	}
}
