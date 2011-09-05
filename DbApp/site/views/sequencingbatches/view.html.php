<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewSequencingBatches extends JView {

	function display($tpl = null) {
                // Assign data to the view
    $items = $this->get('Items');
    $this->assignRef('sequencingbatches', $items);

    if (count($errors = $this->get('Errors'))) {
      JError::raiseError(500, implode('<br />', $errors));
      return false;
    }
 
    parent::display($tpl);

	}

}
