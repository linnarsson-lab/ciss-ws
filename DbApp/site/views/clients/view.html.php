<?php

defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.view');

class DbAppViewClients extends JView {

	function display($tpl = null) {
                // Assign data to the view
    $clients = $this->get('Clients');
    $this->assignRef('clients', $clients);

//                $this->items =& $this->get('Items');
//foreach($this->items as $i => $item): 
//      JError::raiseWarning(500, JText::_('DbAppViewClientsL14:  ' . $item->));
// endforeach; 
//                $this->assignRef('items', $this->items);

                if (count($errors = $this->get('Errors'))) {
                        JError::raiseError(500, implode('<br />', $errors));
                        return false;
                }
 
//                $this->setDocument();
                // Display the view
                parent::display($tpl);

	}


//  protected function setDocument() {
//    $document = JFactory::getDocument();
//    $document->setTitle(JText::_('DbApp Clients'));
//  }


}
